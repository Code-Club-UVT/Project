using System.Text.Json;
using Identity.Services.Outbox;
using Identity.Tests.Support;
using Xunit;

namespace Identity.Tests;

public class OutboxTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly FakeOutboxRepository _repository = new();

    private Outbox Outbox() => new(_repository, _clock);

    [Fact]
    public async Task A_new_account_is_queued_for_core_with_its_details()
    {
        var id = Guid.NewGuid();

        await Outbox().UserRegisteredAsync(id, "ada", "ada@example.org", "Ada", null);

        var message = Assert.Single(_repository.Items);
        Assert.Equal((OutboxTargets.Core, "user-registered", "internal/users"), (message.Target, message.Type, message.Path));
        Assert.Equal(_clock.UtcNow, message.CreatedAt);
        Assert.Equal(_clock.UtcNow, message.NextAttemptAt);
        Assert.Equal(0, message.Attempts);

        var body = JsonDocument.Parse(message.Body).RootElement;
        Assert.Equal(id, body.GetProperty("userId").GetGuid());
        Assert.Equal("ada", body.GetProperty("username").GetString());
        Assert.Equal("ada@example.org", body.GetProperty("email").GetString());
        Assert.Equal("Ada", body.GetProperty("firstName").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastName").ValueKind);
    }

    [Fact]
    public async Task A_confirmation_is_queued_for_core_at_the_users_confirmed_path()
    {
        var id = Guid.NewGuid();

        await Outbox().UserConfirmedAsync(id);

        var message = Assert.Single(_repository.Items);
        Assert.Equal((OutboxTargets.Core, "user-confirmed", $"internal/users/{id}/confirmed"), (message.Target, message.Type, message.Path));
    }

    [Fact]
    public async Task An_email_is_queued_for_the_email_service_and_its_message_id_is_the_outbox_id()
    {
        await Outbox().SendEmailAsync(EmailTemplates.EmailConfirmation, "ada@example.org", new Dictionary<string, string> { ["link"] = "https://x/y" });

        var message = Assert.Single(_repository.Items);
        Assert.Equal((OutboxTargets.Email, EmailTemplates.EmailConfirmation, "internal/emails"), (message.Target, message.Type, message.Path));

        var body = JsonDocument.Parse(message.Body).RootElement;
        Assert.Equal(message.Id, body.GetProperty("messageId").GetGuid());
        Assert.Equal("ada@example.org", body.GetProperty("to").GetString());
        Assert.Equal(EmailTemplates.EmailConfirmation, body.GetProperty("template").GetString());
        Assert.Equal("https://x/y", body.GetProperty("data").GetProperty("link").GetString());
    }
}
