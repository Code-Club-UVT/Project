using CoreApi.Common.Results;
using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Options;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Email;
using CoreApi.Services.Tokens;
using CoreApi.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CoreApi.Services.Auth;

public class AccountService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUserTokenRepository userTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IEmailSender emailSender,
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
            Role = UserRole.User,
            CreatedAt = now,
            UpdatedAt = now,
            Profile = new UserProfile
            {
                FirstName = NullIfBlank(request.FirstName),
                LastName = NullIfBlank(request.LastName)
            }
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await users.CreateAsync(user, cancellationToken);
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.EmailConfirmation, TimeSpan.FromHours(_auth.EmailConfirmationHours), now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailConfirmationAsync(user.Email, BuildLink(_frontend.ConfirmEmailPath, rawToken), cancellationToken);
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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.EmailConfirmation, TimeSpan.FromHours(_auth.EmailConfirmationHours), now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailConfirmationAsync(user.Email, BuildLink(_frontend.ConfirmEmailPath, rawToken), cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = await CreateUserTokenAsync(user.Id, UserTokenType.PasswordReset, TimeSpan.FromHours(_auth.PasswordResetHours), now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendPasswordResetAsync(user.Email, BuildLink(_frontend.ResetPasswordPath, rawToken), cancellationToken);
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
            return UserErrors.NotFound;
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

    private string BuildLink(string path, string rawToken)
    {
        return $"{_frontend.BaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
