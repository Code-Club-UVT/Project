using Identity.Entities;
using Identity.Options;
using Identity.Services.Auth;
using Identity.Services.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Tests.Support;

/// <summary>Real services on top of in-memory repositories, with a clock the test controls.</summary>
public sealed class IdentityFixture
{
    public const string Password = "correct-horse-battery";

    public FakeTimeProvider Clock { get; } = new();
    public FakeUserRepository Users { get; } = new();
    public FakeRefreshTokenRepository RefreshTokens { get; }
    public FakeUserTokenRepository UserTokens { get; }
    public FakeOutbox Outbox { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();

    public JwtOptions Jwt { get; } = new() { Issuer = "identity", Audience = "gateway", AccessTokenMinutes = 15, ProjectTokenMinutes = 5 };
    public RefreshTokenOptions Refresh { get; } = new() { Days = 7, AbsoluteDays = 30, ReuseGraceSeconds = 10 };
    public ThrottleOptions Throttle { get; } = new();
    public AuthOptions Auth { get; } = new();
    public FrontendOptions Frontend { get; } = new() { BaseUrl = "https://example.org/dev/", ConfirmEmailPath = "/confirm-email", ResetPasswordPath = "/reset-password" };

    public IPasswordHasher<User> Hasher { get; set; } = new PasswordHasher<User>();

    public SigningKeyProvider SigningKey { get; }
    public TokenService Tokens { get; }
    public AttemptLimiter Limiter { get; }

    public IdentityFixture()
    {
        RefreshTokens = new FakeRefreshTokenRepository(Users);
        UserTokens = new FakeUserTokenRepository(Users);
        SigningKey = new SigningKeyProvider(Opts.Of(Jwt), new TestEnvironment("Development"), NullLogger<SigningKeyProvider>.Instance);
        Tokens = new TokenService(Opts.Of(Jwt), SigningKey, Clock);
        Limiter = new AttemptLimiter(Opts.Of(Throttle), Clock, NullLogger<AttemptLimiter>.Instance);
    }

    public AuthService AuthService() => new(
        Users, RefreshTokens, UnitOfWork, Hasher, Tokens, Limiter,
        Opts.Of(Refresh), Clock, NullLogger<AuthService>.Instance);

    public AccountService AccountService() => new(
        Users, RefreshTokens, UserTokens, UnitOfWork, Hasher, Tokens, Limiter, Outbox,
        Opts.Of(Auth), Opts.Of(Frontend), Clock, NullLogger<AccountService>.Instance);

    public User AddUser(string username = "ada", string password = Password, bool confirmed = true, bool active = true, string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email ?? $"{username}@example.org",
            EmailConfirmed = confirmed,
            IsActive = active,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        user.PasswordHash = Hasher.HashPassword(user, password);
        Users.Items.Add(user);
        return user;
    }

    /// <summary>Stores a refresh token for the user and returns it with its raw value (what the cookie holds).</summary>
    public (RefreshToken Token, string Raw) AddRefreshToken(User user, Guid? familyId = null, DateTime? familyStartedAt = null, DateTime? expiresAt = null)
    {
        var raw = Tokens.GenerateToken();
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = Tokens.Hash(raw),
            CreatedAt = Clock.UtcNow,
            ExpiresAt = expiresAt ?? Clock.UtcNow.AddDays(7),
            FamilyStartedAt = familyStartedAt ?? Clock.UtcNow
        };
        RefreshTokens.Items.Add(token);
        return (token, raw);
    }
}
