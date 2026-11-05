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
        var user = await users.GetByIdentifierAsync(request.Identifier.Trim(), cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password);
            return AuthErrors.InvalidCredentials;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed || !user.IsActive)
        {
            return AuthErrors.InvalidCredentials;
        }

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

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var token in await refreshTokens.GetActiveByFamilyAsync(familyId, cancellationToken))
        {
            token.RevokedAt = now;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
