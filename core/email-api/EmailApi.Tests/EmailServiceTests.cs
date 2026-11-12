using EmailApi.Models;
using EmailApi.Options;
using EmailApi.Services;
using EmailApi.Templates;
using EmailApi.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmailApi.Tests;

public class EmailServiceTests
{
    private readonly FakeSender _sender = new();
    private readonly FakeSentEmails _sent = new();
    private readonly FakeTimeProvider _clock = new();

    private EmailService Service() => new(
        [new EmailConfirmationTemplate(), new PasswordResetTemplate()],
        _sender,
        _sent,
        Microsoft.Extensions.Options.Options.Create(new SmtpOptions { FromName = "Code Club" }),
        _clock,
        NullLogger<EmailService>.Instance);

    private static SendEmailRequest Request(string template = "email-confirmation", string to = "ada@example.org", Dictionary<string, string>? data = null, Guid? id = null)
        => new() { MessageId = id ?? Guid.NewGuid(), Template = template, To = to, Data = data ?? new() { ["link"] = "https://example.org/confirm?token=abc" } };

    [Fact]
    public async Task A_known_template_is_rendered_and_sent_to_the_recipient()
    {
        var result = await Service().SendAsync(Request(), "identity");

        Assert.True(result.IsSuccess);
        var (to, content) = Assert.Single(_sender.Sent);
        Assert.Equal("ada@example.org", to);
        Assert.Equal("Confirm your email address", content.Subject);
        Assert.Contains("Code Club", content.Html);
        Assert.Contains("https://example.org/confirm?token=abc", content.Text);
    }

    [Fact]
    public async Task The_message_is_recorded_with_who_asked_and_when_but_never_the_body()
    {
        var id = Guid.NewGuid();

        await Service().SendAsync(Request("password-reset", id: id), "identity");

        var record = Assert.Single(_sent.Items);
        Assert.Equal((id, "password-reset", "ada@example.org", "identity", _clock.GetUtcNow().UtcDateTime), (record.MessageId, record.Template, record.To, record.Caller, record.SentAt));
    }

    [Fact]
    public async Task The_same_message_id_is_not_sent_twice()
    {
        var request = Request();
        var service = Service();

        await service.SendAsync(request, "identity");
        var again = await service.SendAsync(request, "identity");

        Assert.True(again.IsSuccess);
        Assert.Single(_sender.Sent);
        Assert.Single(_sent.Items);
    }

    [Fact]
    public async Task Different_message_ids_are_different_emails_even_to_the_same_address()
    {
        var service = Service();

        await service.SendAsync(Request(), "identity");
        await service.SendAsync(Request(), "identity");

        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task An_unknown_template_is_refused_and_nothing_is_sent()
    {
        var result = await Service().SendAsync(Request("newsletter"), "identity");

        Assert.Equal("Email.UnknownTemplate", result.Error!.Code);
        Assert.Contains("newsletter", result.Error.Message);
        Assert.Empty(_sender.Sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_or_blank_link_is_refused(string? link)
    {
        var data = link is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["link"] = link };

        var result = await Service().SendAsync(Request(data: data), "identity");

        Assert.Equal("Email.MissingData", result.Error!.Code);
        Assert.Contains("link", result.Error.Message);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task No_data_at_all_is_a_missing_link()
    {
        var request = Request() with { Data = null };

        var result = await Service().SendAsync(request, "identity");

        Assert.Equal("Email.MissingData", result.Error!.Code);
    }

    [Fact]
    public async Task A_link_that_is_not_http_is_refused()
    {
        var result = await Service().SendAsync(Request(data: new() { ["link"] = "javascript:alert(1)" }), "identity");

        Assert.Equal("Email.InvalidData", result.Error!.Code);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task When_the_mail_server_fails_the_error_reaches_the_caller_and_nothing_is_recorded()
    {
        _sender.Failure = new InvalidOperationException("SMTP is down");
        var request = Request();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SendAsync(request, "identity"));

        Assert.Empty(_sent.Items);

        // The caller retries with the same id once the server is back, and the email goes out then.
        _sender.Failure = null;
        Assert.True((await Service().SendAsync(request, "identity")).IsSuccess);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task Losing_the_race_to_record_the_id_is_still_a_success()
    {
        _sent.LoseTheRace = true;

        var result = await Service().SendAsync(Request(), "identity");

        Assert.True(result.IsSuccess);
        Assert.Single(_sender.Sent);
    }
}
