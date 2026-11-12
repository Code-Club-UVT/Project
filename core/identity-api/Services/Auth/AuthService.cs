using PlatformKit.Results;
using Identity.Entities;
using Identity.Mappings;
using Identity.Models.Requests;
using Identity.Models.Responses;
using Identity.Options;
using Identity.Repositories.Abstractions;
using Identity.Services.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Services.Auth;

public class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    IAttemptLimiter limiter,
    IOptions<RefreshTokenOptions> refreshOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    // Verified against when the user does not exist, so unknown users cost the same time as wrong passwords.
    private static readonly User DummyUser = new();
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, "dummy-password-for-timing");

    private readonly RefreshTokenOptions _refresh = refreshOptions.Value;

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = request.Identifier.Trim();

        // Counted per identifier whether or not the account exists, so this answer never reveals which accounts exist.
        // A refused attempt is not counted again, so the block ends when the oldest failure leaves the window.
        if (limiter.IsLoginBlocked(identifier))
        {
            return AuthErrors.TooManyAttempts;
        }

        var user = await users.GetByIdentifierAsync(identifier, cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password);
            limiter.RecordLoginFailure(identifier);
            return AuthErrors.InvalidCredentials;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed || !user.IsActive)
        {
            limiter.RecordLoginFailure(identifier);
            return AuthErrors.InvalidCredentials;
        }

        limiter.ClearLogin(identifier);

        // Only reported after the password is verified, so it does not reveal which accounts exist.
        if (!user.EmailConfirmed)
        {
            return AuthErrors.EmailNotConfirmed;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = now;
        }

        var (token, rawToken) = await CreateRefreshTokenAsync(user.Id, Guid.NewGuid(), now, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateSession(user, token, rawToken);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var stored = await refreshTokens.GetByHashAsync(tokenService.Hash(refreshToken), cancellationToken);
        if (stored is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (stored.RevokedAt is not null)
        {
            // Two refreshes with the same cookie at the same moment (two tabs): the second came right after the first rotated
            // the token. The first response already set the new cookie, so this one only gets an access token.
            if (await IsRotationRaceAsync(stored, now, cancellationToken))
            {
                logger.LogInformation("Refresh token of user {UserId} reused {Seconds:N1}s after its rotation; access token only", stored.UserId, (now - stored.RevokedAt.Value).TotalSeconds);
                return CreateAccessOnlySession(stored.User);
            }

            // A revoked token came back: assume it was stolen and end every session of its family.
            logger.LogWarning("Reuse of a revoked refresh token for user {UserId}; revoking family {FamilyId}", stored.UserId, stored.FamilyId);
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var user = stored.User;
        if (stored.ExpiresAt <= now || !user.IsActive || !user.EmailConfirmed)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var (replacement, rawToken) = await CreateRefreshTokenAsync(user.Id, stored.FamilyId, stored.FamilyStartedAt, now, cancellationToken);
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = replacement.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateSession(user, replacement, rawToken);
    }

    public async Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Success();
        }

        var stored = await refreshTokens.GetByHashAsync(tokenService.Hash(refreshToken), cancellationToken);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<(RefreshToken Token, string RawToken)> CreateRefreshTokenAsync(
        Guid userId, Guid familyId, DateTime familyStartedAt, DateTime now, CancellationToken cancellationToken)
    {
        var rawToken = tokenService.GenerateToken();
        var expiresAt = Min(now.AddDays(_refresh.Days), familyStartedAt.AddDays(_refresh.AbsoluteDays));

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenService.Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            FamilyStartedAt = familyStartedAt
        };

        await refreshTokens.CreateAsync(token, cancellationToken);
        return (token, rawToken);
    }

    private AuthSession CreateSession(User user, RefreshToken token, string rawToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var response = new AuthResponse(accessToken.Token, accessToken.ExpiresAt, user.ToResponse());

        return new AuthSession(response, rawToken, token.ExpiresAt);
    }

    /// <summary>
    /// The token was rotated (it has a replacement), a moment ago, and the replacement is still valid, and the account is
    /// usable. A token revoked by logout or a password change has no replacement, so it never qualifies.
    /// </summary>
    private async Task<bool> IsRotationRaceAsync(RefreshToken stored, DateTime now, CancellationToken cancellationToken)
    {
        if (_refresh.ReuseGraceSeconds <= 0
            || stored.RevokedAt is not { } revokedAt
            || stored.ReplacedByTokenId is not { } replacementId
            || now - revokedAt > TimeSpan.FromSeconds(_refresh.ReuseGraceSeconds)
            || stored.ExpiresAt <= now
            || !stored.User.IsActive
            || !stored.User.EmailConfirmed)
        {
            return false;
        }

        var active = await refreshTokens.GetActiveByFamilyAsync(stored.FamilyId, cancellationToken);
        return active.Any(token => token.Id == replacementId && token.ExpiresAt > now);
    }

    private AuthSession CreateAccessOnlySession(User user)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthSession(new AuthResponse(accessToken.Token, accessToken.ExpiresAt, user.ToResponse()), null, null);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetActiveByFamilyAsync(familyId, cancellationToken))
        {
            token.RevokedAt = now;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
