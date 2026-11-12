using Identity.Entities;
using Identity.Models.Requests;
using Identity.Services.Auth;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Identity.Tests;

public class AuthServiceLoginTests
{
    private readonly IdentityFixture _f = new();

    private static LoginRequest Login(string identifier, string password = IdentityFixture.Password)
        => new() { Identifier = identifier, Password = password };

    private sealed class RehashHasher : IPasswordHasher<User>
    {
        public string HashPassword(User user, string password) => "rehashed:" + password;

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
            => PasswordVerificationResult.SuccessRehashNeeded;
    }

    [Fact]
    public async Task A_correct_login_returns_an_access_token_and_a_refresh_token()
    {
        var user = _f.AddUser("ada");

        var result = await _f.AuthService().LoginAsync(Login("ada"));

        Assert.True(result.IsSuccess);
        var session = result.Value;
        Assert.Equal(user.Id, session.Response.User.Id);
        Assert.NotNull(session.RefreshToken);
        Assert.Equal(_f.Clock.UtcNow.AddDays(7), session.RefreshTokenExpiresAt);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(session.Response.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Contains("gateway", jwt.Audiences);
        Assert.Equal("identity", jwt.Issuer);
        Assert.Equal(_f.Clock.UtcNow.AddMinutes(15), session.Response.ExpiresAt);
    }

    [Fact]
    public async Task Only_the_hash_of_the_refresh_token_is_stored()
    {
        _f.AddUser("ada");

        var session = (await _f.AuthService().LoginAsync(Login("ada"))).Value;

        var stored = Assert.Single(_f.RefreshTokens.Items);
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(_f.Tokens.Hash(session.RefreshToken!), stored.TokenHash);
        Assert.Equal(1, _f.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Each_login_starts_its_own_token_family()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();

        await service.LoginAsync(Login("ada"));
        await service.LoginAsync(Login("ada"));

        Assert.Equal(2, _f.RefreshTokens.Items.Select(t => t.FamilyId).Distinct().Count());
    }

    [Fact]
    public async Task The_email_works_as_the_identifier()
    {
        _f.AddUser("ada", email: "ada@example.org");

        var result = await _f.AuthService().LoginAsync(Login("ada@example.org"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Surrounding_spaces_in_the_identifier_are_ignored()
    {
        _f.AddUser("ada");

        Assert.True((await _f.AuthService().LoginAsync(Login("  ada  "))).IsSuccess);
    }

    [Fact]
    public async Task An_unknown_user_and_a_wrong_password_give_the_same_error()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();

        var unknown = await service.LoginAsync(Login("nobody"));
        var wrong = await service.LoginAsync(Login("ada", "wrong-password"));

        Assert.Same(AuthErrors.InvalidCredentials, unknown.Error);
        Assert.Same(AuthErrors.InvalidCredentials, wrong.Error);
        Assert.Empty(_f.RefreshTokens.Items);
    }

    [Fact]
    public async Task A_disabled_account_gets_the_same_error_even_with_the_right_password()
    {
        _f.AddUser("ada", active: false);

        var result = await _f.AuthService().LoginAsync(Login("ada"));

        Assert.Same(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task An_unconfirmed_email_is_reported_only_after_the_password_is_right()
    {
        _f.AddUser("ada", confirmed: false);
        var service = _f.AuthService();

        var right = await service.LoginAsync(Login("ada"));
        var wrong = await service.LoginAsync(Login("ada", "wrong-password"));

        Assert.Same(AuthErrors.EmailNotConfirmed, right.Error);
        Assert.Same(AuthErrors.InvalidCredentials, wrong.Error);
    }

    [Fact]
    public async Task A_hash_that_needs_an_upgrade_is_replaced_at_login()
    {
        _f.Hasher = new RehashHasher();
        var user = _f.AddUser("ada");
        _f.Clock.Advance(TimeSpan.FromHours(1));

        await _f.AuthService().LoginAsync(Login("ada"));

        Assert.Equal("rehashed:" + IdentityFixture.Password, user.PasswordHash);
        Assert.Equal(_f.Clock.UtcNow, user.UpdatedAt);
    }

    [Fact]
    public async Task After_too_many_failures_even_the_right_password_is_refused()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();
        for (var i = 0; i < _f.Throttle.LoginFailures; i++)
        {
            await service.LoginAsync(Login("ada", "wrong-password"));
        }

        var result = await service.LoginAsync(Login("ada"));

        Assert.Same(AuthErrors.TooManyAttempts, result.Error);
        Assert.Empty(_f.RefreshTokens.Items);
    }

    [Fact]
    public async Task Unknown_names_are_counted_like_known_ones()
    {
        var service = _f.AuthService();
        for (var i = 0; i < _f.Throttle.LoginFailures; i++)
        {
            await service.LoginAsync(Login("nobody", "wrong-password"));
        }

        var result = await service.LoginAsync(Login("nobody"));

        Assert.Same(AuthErrors.TooManyAttempts, result.Error);
    }

    [Fact]
    public async Task The_block_ends_after_the_window_and_the_user_can_log_in_again()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();
        for (var i = 0; i < _f.Throttle.LoginFailures; i++)
        {
            await service.LoginAsync(Login("ada", "wrong-password"));
        }

        _f.Clock.Advance(TimeSpan.FromMinutes(_f.Throttle.LoginWindowMinutes + 1));

        Assert.True((await service.LoginAsync(Login("ada"))).IsSuccess);
    }

    [Fact]
    public async Task A_refused_attempt_is_not_counted_so_the_block_does_not_grow()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();
        for (var i = 0; i < _f.Throttle.LoginFailures; i++)
        {
            await service.LoginAsync(Login("ada", "wrong-password"));
        }

        _f.Clock.Advance(TimeSpan.FromMinutes(10));
        await service.LoginAsync(Login("ada", "wrong-password"));
        _f.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.True((await service.LoginAsync(Login("ada"))).IsSuccess);
    }

    [Fact]
    public async Task A_successful_login_clears_the_failures()
    {
        _f.AddUser("ada");
        var service = _f.AuthService();
        for (var i = 0; i < _f.Throttle.LoginFailures - 1; i++)
        {
            await service.LoginAsync(Login("ada", "wrong-password"));
        }

        await service.LoginAsync(Login("ada"));
        for (var i = 0; i < _f.Throttle.LoginFailures - 1; i++)
        {
            await service.LoginAsync(Login("ada", "wrong-password"));
        }

        Assert.True((await service.LoginAsync(Login("ada"))).IsSuccess);
    }
}
