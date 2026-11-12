using PlatformKit.Results;
using Identity.Entities;
using Identity.Enums;
using Identity.Mappings;
using Identity.Models.Requests;
using Identity.Models.Responses;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Identity.Services.Outbox;
using Identity.Services.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Services.Auth;

public class AccountService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUserTokenRepository userTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IAttemptLimiter limiter,
    IOutbox outbox,
    IOptions<AuthOptions> authOptions,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider,
    ILogger<AccountService> logger) : IAccountService
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly FrontendOptions _frontend = frontendOptions.Value;

    public async Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await users.UsernameExistsAsync(username, cancellationToken))
        {
            return AuthErrors.UsernameTaken;
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return AuthErrors.EmailTaken;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        // Counts this first email, so a "resend" right after registering waits for the minimum interval too. When the
        // service has sent all the emails it may for now, the account is not created: its owner could not confirm it.
        if (limiter.TryAcquireEmail(user.Email) == EmailPermit.GlobalLimited)
        {
            return AuthErrors.EmailLimitReached;
        }

        await users.CreateAsync(user, cancellationToken);
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.EmailConfirmation, TimeSpan.FromHours(_auth.EmailConfirmationHours), now, cancellationToken);

        // The name is not stored here: the core API keeps the profile. Both calls commit with the user.
        await outbox.UserRegisteredAsync(user.Id, user.Username, user.Email, NullIfBlank(request.FirstName), NullIfBlank(request.LastName), cancellationToken);
        await QueueLinkEmailAsync(EmailTemplates.EmailConfirmation, user.Email, _frontend.ConfirmEmailPath, rawToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} registered", user.Id);

        return Result.Success(user.ToResponse());
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = await FindValidTokenAsync(request.Token, UserTokenType.EmailConfirmation, now, cancellationToken);
        if (token is null)
        {
            return AuthErrors.InvalidToken;
        }

        token.UsedAt = now;
        token.User.EmailConfirmed = true;
        token.User.UpdatedAt = now;

        // The core API grants the projects' default roles when it hears about the confirmation.
        await outbox.UserConfirmedAsync(token.UserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || user.EmailConfirmed || !user.IsActive)
        {
            return Result.Success();
        }

        // Too soon after the last email to this address, or the service's total is used up: answer as if sent (nothing
        // reveals the limit to a stranger), and keep the link the user already has valid.
        if (limiter.TryAcquireEmail(user.Email) != EmailPermit.Allowed)
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.EmailConfirmation, TimeSpan.FromHours(_auth.EmailConfirmationHours), now, cancellationToken);
        await QueueLinkEmailAsync(EmailTemplates.EmailConfirmation, user.Email, _frontend.ConfirmEmailPath, rawToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        if (limiter.TryAcquireEmail(user.Email) != EmailPermit.Allowed)
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.PasswordReset, TimeSpan.FromHours(_auth.PasswordResetHours), now, cancellationToken);
        await QueueLinkEmailAsync(EmailTemplates.PasswordReset, user.Email, _frontend.ResetPasswordPath, rawToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = await FindValidTokenAsync(request.Token, UserTokenType.PasswordReset, now, cancellationToken);
        if (token is null)
        {
            return AuthErrors.InvalidToken;
        }

        var user = token.User;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = now;
        token.UsedAt = now;
        await RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Password reset for user {UserId}", user.Id);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return AuthErrors.UserNotFound;
        }

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return AuthErrors.InvalidCurrentPassword;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = now;
        await RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Password changed for user {UserId}", user.Id);
        return Result.Success();
    }

    /// <summary>Creates a token for the user and returns the raw value. Older unused tokens of the same type stop working.</summary>
    private async Task<string> CreateUserTokenAsync(Guid userId, UserTokenType type, TimeSpan lifetime, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var previous in await userTokens.GetUnusedAsync(userId, type, cancellationToken))
        {
            previous.UsedAt = now;
        }

        var rawToken = tokenService.GenerateToken();
        await userTokens.CreateAsync(new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            TokenHash = tokenService.Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime)
        }, cancellationToken);

        return rawToken;
    }

    private async Task<UserToken?> FindValidTokenAsync(string rawToken, UserTokenType type, DateTime now, CancellationToken cancellationToken)
    {
        var token = await userTokens.GetByHashAsync(tokenService.Hash(rawToken), type, cancellationToken);

        return token is null || token.UsedAt is not null || token.ExpiresAt <= now ? null : token;
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetActiveByUserAsync(userId, cancellationToken))
        {
            token.RevokedAt = now;
        }
    }

    private Task QueueLinkEmailAsync(string template, string to, string path, string rawToken, CancellationToken cancellationToken)
    {
        var link = $"{_frontend.BaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(rawToken)}";
        return outbox.SendEmailAsync(template, to, new Dictionary<string, string> { [EmailTemplates.LinkKey] = link }, cancellationToken);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
