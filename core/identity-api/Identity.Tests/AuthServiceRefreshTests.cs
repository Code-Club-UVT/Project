using Identity.Services.Auth;
using Identity.Tests.Support;
using Xunit;

namespace Identity.Tests;

public class AuthServiceRefreshTests
{
    private readonly IdentityFixture _f = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_token_is_refused(string? token)
    {
        var result = await _f.AuthService().RefreshAsync(token);

        Assert.Same(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task An_unknown_token_is_refused()
    {
        _f.AddUser("ada");

        var result = await _f.AuthService().RefreshAsync("not-a-token");

        Assert.Same(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task A_valid_token_is_rotated_within_its_family()
    {
        var user = _f.AddUser("ada");
        var (old, raw) = _f.AddRefreshToken(user);
        _f.Clock.Advance(TimeSpan.FromHours(1));

        var result = await _f.AuthService().RefreshAsync(raw);

        Assert.True(result.IsSuccess);
        var session = result.Value;
        Assert.NotNull(session.RefreshToken);
        Assert.NotEqual(raw, session.RefreshToken);
        Assert.Equal(_f.Clock.UtcNow, old.RevokedAt);

        var replacement = _f.RefreshTokens.Items.Single(t => t.Id != old.Id);
        Assert.Equal(replacement.Id, old.ReplacedByTokenId);
        Assert.Equal(old.FamilyId, replacement.FamilyId);
        Assert.Equal(old.FamilyStartedAt, replacement.FamilyStartedAt);
        Assert.Equal(_f.Tokens.Hash(session.RefreshToken!), replacement.TokenHash);
        Assert.Equal(_f.Clock.UtcNow.AddDays(7), replacement.ExpiresAt);
        Assert.Null(replacement.RevokedAt);
    }

    [Fact]
    public async Task A_new_access_token_for_the_same_user_comes_with_it()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);

        var session = (await _f.AuthService().RefreshAsync(raw)).Value;

        Assert.Equal(user.Id, session.Response.User.Id);
        Assert.False(string.IsNullOrEmpty(session.Response.AccessToken));
    }

    [Fact]
    public async Task The_session_cannot_be_extended_beyond_the_absolute_lifetime()
    {
        var user = _f.AddUser("ada");
        var started = _f.Clock.UtcNow.AddDays(-29);
        var (_, raw) = _f.AddRefreshToken(user, familyStartedAt: started, expiresAt: _f.Clock.UtcNow.AddDays(1));

        var session = (await _f.AuthService().RefreshAsync(raw)).Value;

        Assert.Equal(started.AddDays(30), session.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task An_expired_token_is_refused_and_not_rotated()
    {
        var user = _f.AddUser("ada");
        var (old, raw) = _f.AddRefreshToken(user, expiresAt: _f.Clock.UtcNow.AddMinutes(-1));

        var result = await _f.AuthService().RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Null(old.RevokedAt);
        Assert.Single(_f.RefreshTokens.Items);
    }

    [Fact]
    public async Task A_disabled_or_unconfirmed_user_cannot_refresh()
    {
        var disabled = _f.AddUser("disabled", active: false);
        var unconfirmed = _f.AddUser("unconfirmed", confirmed: false);
        var (_, rawDisabled) = _f.AddRefreshToken(disabled);
        var (_, rawUnconfirmed) = _f.AddRefreshToken(unconfirmed);
        var service = _f.AuthService();

        Assert.Same(AuthErrors.InvalidRefreshToken, (await service.RefreshAsync(rawDisabled)).Error);
        Assert.Same(AuthErrors.InvalidRefreshToken, (await service.RefreshAsync(rawUnconfirmed)).Error);
    }

    [Fact]
    public async Task A_stolen_token_replayed_after_the_grace_window_ends_the_whole_family()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        var rotated = (await service.RefreshAsync(raw)).Value;

        _f.Clock.Advance(TimeSpan.FromSeconds(_f.Refresh.ReuseGraceSeconds + 1));
        var replay = await service.RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, replay.Error);
        Assert.All(_f.RefreshTokens.Items, t => Assert.NotNull(t.RevokedAt));
        Assert.Same(AuthErrors.InvalidRefreshToken, (await service.RefreshAsync(rotated.RefreshToken)).Error);
    }

    [Fact]
    public async Task Other_families_of_the_same_user_are_left_alone()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var (other, _) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        await service.RefreshAsync(raw);

        _f.Clock.Advance(TimeSpan.FromMinutes(1));
        await service.RefreshAsync(raw);

        Assert.Null(other.RevokedAt);
    }

    // The same cookie sent twice at the same moment (two tabs): the second call comes just after the first rotated it.

    [Fact]
    public async Task A_token_reused_inside_the_grace_window_gets_an_access_token_but_no_new_refresh_token()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        var first = (await service.RefreshAsync(raw)).Value;
        _f.Clock.Advance(TimeSpan.FromSeconds(2));

        var second = await service.RefreshAsync(raw);

        Assert.True(second.IsSuccess);
        Assert.Null(second.Value.RefreshToken);
        Assert.Null(second.Value.RefreshTokenExpiresAt);
        Assert.False(string.IsNullOrEmpty(second.Value.Response.AccessToken));
        Assert.Equal(user.Id, second.Value.Response.User.Id);

        // Nothing was revoked and no second replacement was created: the cookie of the first response stays valid.
        Assert.Equal(2, _f.RefreshTokens.Items.Count);
        Assert.Single(_f.RefreshTokens.Items, t => t.RevokedAt is null);
        Assert.True((await service.RefreshAsync(first.RefreshToken)).IsSuccess);
    }

    [Fact]
    public async Task The_window_includes_its_last_second()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        await service.RefreshAsync(raw);
        _f.Clock.Advance(TimeSpan.FromSeconds(_f.Refresh.ReuseGraceSeconds));

        Assert.True((await service.RefreshAsync(raw)).IsSuccess);
    }

    [Fact]
    public async Task With_the_grace_switched_off_a_reused_token_ends_the_family()
    {
        _f.Refresh.ReuseGraceSeconds = 0;
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        await service.RefreshAsync(raw);

        var second = await service.RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, second.Error);
        Assert.All(_f.RefreshTokens.Items, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task A_token_revoked_by_logout_never_gets_the_grace()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        await service.LogoutAsync(raw);

        var result = await service.RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task No_grace_when_the_replacement_has_been_revoked_already()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        var first = (await service.RefreshAsync(raw)).Value;
        await service.LogoutAsync(first.RefreshToken);

        var second = await service.RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, second.Error);
    }

    [Fact]
    public async Task No_grace_when_the_user_was_disabled_in_the_meantime()
    {
        var user = _f.AddUser("ada");
        var (_, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();
        await service.RefreshAsync(raw);
        user.IsActive = false;

        var second = await service.RefreshAsync(raw);

        Assert.Same(AuthErrors.InvalidRefreshToken, second.Error);
    }

    [Fact]
    public async Task Logout_revokes_the_token_and_is_harmless_to_repeat()
    {
        var user = _f.AddUser("ada");
        var (token, raw) = _f.AddRefreshToken(user);
        var service = _f.AuthService();

        Assert.True((await service.LogoutAsync(raw)).IsSuccess);
        var revokedAt = token.RevokedAt;
        _f.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True((await service.LogoutAsync(raw)).IsSuccess);

        Assert.NotNull(revokedAt);
        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task Logout_always_succeeds(string? raw)
    {
        Assert.True((await _f.AuthService().LogoutAsync(raw)).IsSuccess);
    }
}
