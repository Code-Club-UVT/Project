using Identity.Entities;
using Identity.Enums;
using Identity.Models.Requests;
using Identity.Services.Auth;
using Identity.Services.Outbox;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Identity.Tests;

public class AccountServiceTests
{
    private readonly IdentityFixture _f = new();

    private static RegisterRequest Register(string username = "ada", string email = "ada@example.org", string password = "a-long-enough-password")
        => new() { Username = username, Email = email, Password = password };

    /// <summary>The raw token inside the link of the last email.</summary>
    private static string TokenOf(FakeOutbox.Email email) => email.Data[EmailTemplates.LinkKey].Split("?token=")[1];

    // ---- register

    [Fact]
    public async Task Registering_creates_an_unconfirmed_account_with_a_hashed_password()
    {
        var result = await _f.AccountService().RegisterAsync(Register());

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_f.Users.Items);
        Assert.Equal("ada", user.Username);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.IsActive);
        Assert.NotEqual("a-long-enough-password", user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _f.Hasher.VerifyHashedPassword(user, user.PasswordHash, "a-long-enough-password"));
        Assert.Equal(user.Id, result.Value.Id);
        Assert.False(result.Value.EmailConfirmed);
    }

    [Fact]
    public async Task Registering_tells_core_and_queues_one_confirmation_email_in_one_save()
    {
        var request = Register() with { FirstName = "Ada", LastName = " " };

        await _f.AccountService().RegisterAsync(request);

        var user = _f.Users.Items.Single();
        var (id, username, email, first, last) = Assert.Single(_f.Outbox.Registered);
        Assert.Equal((user.Id, "ada", "ada@example.org", "Ada"), (id, username, email, first));
        Assert.Null(last);

        var mail = Assert.Single(_f.Outbox.Emails);
        Assert.Equal(EmailTemplates.EmailConfirmation, mail.Template);
        Assert.Equal("ada@example.org", mail.To);
        Assert.StartsWith("https://example.org/dev/confirm-email?token=", mail.Data[EmailTemplates.LinkKey]);
        Assert.Equal(1, _f.UnitOfWork.Saves);
    }

    [Fact]
    public async Task The_emailed_token_is_stored_only_as_a_hash_and_expires_in_a_day()
    {
        await _f.AccountService().RegisterAsync(Register());

        var raw = TokenOf(_f.Outbox.Emails.Single());
        var stored = Assert.Single(_f.UserTokens.Items);
        Assert.Equal(_f.Tokens.Hash(raw), stored.TokenHash);
        Assert.Equal(UserTokenType.EmailConfirmation, stored.Type);
        Assert.Equal(_f.Clock.UtcNow.AddHours(24), stored.ExpiresAt);
    }

    [Fact]
    public async Task Names_are_trimmed()
    {
        await _f.AccountService().RegisterAsync(Register(username: "  ada  ", email: " ada@example.org "));

        var user = _f.Users.Items.Single();
        Assert.Equal("ada", user.Username);
        Assert.Equal("ada@example.org", user.Email);
    }

    [Fact]
    public async Task A_taken_username_or_email_is_a_conflict_and_nothing_is_queued()
    {
        _f.AddUser("ada", email: "ada@example.org");
        var service = _f.AccountService();

        var username = await service.RegisterAsync(Register(username: "ADA", email: "other@example.org"));
        var email = await service.RegisterAsync(Register(username: "grace", email: "ADA@example.org"));

        Assert.Same(AuthErrors.UsernameTaken, username.Error);
        Assert.Same(AuthErrors.EmailTaken, email.Error);
        Assert.Single(_f.Users.Items);
        Assert.Empty(_f.Outbox.Registered);
        Assert.Empty(_f.Outbox.Emails);
    }

    // ---- confirm email

    [Fact]
    public async Task Confirming_with_the_emailed_token_confirms_the_account_and_tells_core()
    {
        var service = _f.AccountService();
        await service.RegisterAsync(Register());
        var raw = TokenOf(_f.Outbox.Emails.Single());
        _f.Clock.Advance(TimeSpan.FromHours(1));

        var result = await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw });

        Assert.True(result.IsSuccess);
        var user = _f.Users.Items.Single();
        Assert.True(user.EmailConfirmed);
        Assert.Equal(_f.Clock.UtcNow, user.UpdatedAt);
        Assert.Equal(user.Id, Assert.Single(_f.Outbox.Confirmed));
        Assert.Equal(_f.Clock.UtcNow, _f.UserTokens.Items.Single().UsedAt);
    }

    [Fact]
    public async Task A_confirmation_token_works_only_once()
    {
        var service = _f.AccountService();
        await service.RegisterAsync(Register());
        var raw = TokenOf(_f.Outbox.Emails.Single());
        await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw });

        var again = await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw });

        Assert.Same(AuthErrors.InvalidToken, again.Error);
        Assert.Single(_f.Outbox.Confirmed);
    }

    [Fact]
    public async Task An_expired_or_unknown_confirmation_token_is_refused()
    {
        var service = _f.AccountService();
        await service.RegisterAsync(Register());
        var raw = TokenOf(_f.Outbox.Emails.Single());
        _f.Clock.Advance(TimeSpan.FromHours(25));

        Assert.Same(AuthErrors.InvalidToken, (await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw })).Error);
        Assert.Same(AuthErrors.InvalidToken, (await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = "nope" })).Error);
        Assert.False(_f.Users.Items.Single().EmailConfirmed);
    }

    [Fact]
    public async Task A_password_reset_token_cannot_confirm_an_email()
    {
        var user = _f.AddUser("ada");
        var service = _f.AccountService();
        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });
        var raw = TokenOf(_f.Outbox.Emails.Single());

        var result = await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw });

        Assert.Same(AuthErrors.InvalidToken, result.Error);
    }

    // ---- resend confirmation

    [Fact]
    public async Task Resending_to_an_unconfirmed_account_queues_a_new_link_and_retires_the_old_one()
    {
        var user = _f.AddUser("ada", confirmed: false);
        var service = _f.AccountService();
        await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = user.Email });
        var first = TokenOf(_f.Outbox.Emails.Single());
        _f.Clock.Advance(TimeSpan.FromMinutes(3));

        await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = user.Email });

        Assert.Equal(2, _f.Outbox.Emails.Count);
        var second = TokenOf(_f.Outbox.Emails[1]);
        Assert.NotEqual(first, second);
        Assert.Same(AuthErrors.InvalidToken, (await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = first })).Error);
        Assert.True((await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = second })).IsSuccess);
    }

    [Fact]
    public async Task Resending_always_answers_success_and_sends_nothing_when_there_is_nothing_to_send()
    {
        _f.AddUser("done", confirmed: true);
        _f.AddUser("off", confirmed: false, active: false);
        var service = _f.AccountService();

        Assert.True((await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "nobody@example.org" })).IsSuccess);
        Assert.True((await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "done@example.org" })).IsSuccess);
        Assert.True((await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "off@example.org" })).IsSuccess);

        Assert.Empty(_f.Outbox.Emails);
        Assert.Empty(_f.UserTokens.Items);
    }

    [Fact]
    public async Task A_resend_too_soon_answers_success_sends_nothing_and_keeps_the_link_that_was_sent()
    {
        var service = _f.AccountService();
        await service.RegisterAsync(Register());
        var raw = TokenOf(_f.Outbox.Emails.Single());
        _f.Clock.Advance(TimeSpan.FromSeconds(30));

        var result = await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "ada@example.org" });

        Assert.True(result.IsSuccess);
        Assert.Single(_f.Outbox.Emails);
        Assert.Single(_f.UserTokens.Items);
        Assert.True((await service.ConfirmEmailAsync(new ConfirmEmailRequest { Token = raw })).IsSuccess);
    }

    // ---- forgot and reset password

    [Fact]
    public async Task Forgot_password_queues_a_reset_link_for_a_known_account()
    {
        var user = _f.AddUser("ada");

        var result = await _f.AccountService().ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

        Assert.True(result.IsSuccess);
        var mail = Assert.Single(_f.Outbox.Emails);
        Assert.Equal(EmailTemplates.PasswordReset, mail.Template);
        Assert.StartsWith("https://example.org/dev/reset-password?token=", mail.Data[EmailTemplates.LinkKey]);
        var stored = Assert.Single(_f.UserTokens.Items);
        Assert.Equal(UserTokenType.PasswordReset, stored.Type);
        Assert.Equal(_f.Clock.UtcNow.AddHours(1), stored.ExpiresAt);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_or_disabled_accounts()
    {
        _f.AddUser("off", active: false);
        var service = _f.AccountService();

        Assert.True((await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "nobody@example.org" })).IsSuccess);
        Assert.True((await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "off@example.org" })).IsSuccess);

        Assert.Empty(_f.Outbox.Emails);
    }

    [Fact]
    public async Task Forgot_password_is_limited_per_address_but_still_answers_success()
    {
        var user = _f.AddUser("ada");
        var service = _f.AccountService();
        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

        var second = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

        Assert.True(second.IsSuccess);
        Assert.Single(_f.Outbox.Emails);
        Assert.Single(_f.UserTokens.Items);
    }

    [Fact]
    public async Task At_most_five_emails_an_hour_go_to_one_address()
    {
        var user = _f.AddUser("ada");
        var service = _f.AccountService();

        for (var i = 0; i < 8; i++)
        {
            await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });
            _f.Clock.Advance(TimeSpan.FromMinutes(3));
        }

        Assert.Equal(_f.Throttle.EmailsPerHour, _f.Outbox.Emails.Count);
    }

    [Fact]
    public async Task Resetting_sets_the_new_password_uses_the_token_and_ends_every_session()
    {
        var user = _f.AddUser("ada");
        _f.AddRefreshToken(user);
        _f.AddRefreshToken(user);
        var service = _f.AccountService();
        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });
        var raw = TokenOf(_f.Outbox.Emails.Single());

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest { Token = raw, NewPassword = "a-brand-new-password" });

        Assert.True(result.IsSuccess);
        Assert.Equal(PasswordVerificationResult.Success, _f.Hasher.VerifyHashedPassword(user, user.PasswordHash, "a-brand-new-password"));
        Assert.All(_f.RefreshTokens.Items, t => Assert.NotNull(t.RevokedAt));
        Assert.NotNull(_f.UserTokens.Items.Single().UsedAt);
    }

    [Fact]
    public async Task A_reset_token_works_once_and_not_after_an_hour()
    {
        var user = _f.AddUser("ada");
        var service = _f.AccountService();
        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });
        var raw = TokenOf(_f.Outbox.Emails.Single());
        await service.ResetPasswordAsync(new ResetPasswordRequest { Token = raw, NewPassword = "a-brand-new-password" });

        var again = await service.ResetPasswordAsync(new ResetPasswordRequest { Token = raw, NewPassword = "another-password-1" });

        Assert.Same(AuthErrors.InvalidToken, again.Error);

        _f.Clock.Advance(TimeSpan.FromMinutes(5));
        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });
        var late = TokenOf(_f.Outbox.Emails[1]);
        _f.Clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Same(AuthErrors.InvalidToken, (await service.ResetPasswordAsync(new ResetPasswordRequest { Token = late, NewPassword = "another-password-1" })).Error);
    }

    // ---- change password

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_ends_every_session()
    {
        var user = _f.AddUser("ada");
        _f.AddRefreshToken(user);
        var service = _f.AccountService();

        var wrong = await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest { CurrentPassword = "not-it", NewPassword = "a-brand-new-password" });
        Assert.Same(AuthErrors.InvalidCurrentPassword, wrong.Error);
        Assert.Null(_f.RefreshTokens.Items.Single().RevokedAt);

        var ok = await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest { CurrentPassword = IdentityFixture.Password, NewPassword = "a-brand-new-password" });
        Assert.True(ok.IsSuccess);
        Assert.Equal(PasswordVerificationResult.Success, _f.Hasher.VerifyHashedPassword(user, user.PasswordHash, "a-brand-new-password"));
        Assert.NotNull(_f.RefreshTokens.Items.Single().RevokedAt);
    }

    [Fact]
    public async Task Changing_the_password_of_an_unknown_or_disabled_user_is_refused()
    {
        var off = _f.AddUser("off", active: false);
        var service = _f.AccountService();
        var request = new ChangePasswordRequest { CurrentPassword = IdentityFixture.Password, NewPassword = "a-brand-new-password" };

        Assert.Same(AuthErrors.UserNotFound, (await service.ChangePasswordAsync(Guid.NewGuid(), request)).Error);
        Assert.Same(AuthErrors.UserNotFound, (await service.ChangePasswordAsync(off.Id, request)).Error);
    }
}
