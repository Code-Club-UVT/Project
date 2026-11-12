using System.Text.Json;
using Identity.Entities;
using Identity.Repositories.Abstractions;

namespace Identity.Services.Outbox;

public class Outbox(IOutboxRepository messages, TimeProvider timeProvider) : IOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task UserRegisteredAsync(Guid userId, string username, string email, string? firstName, string? lastName, CancellationToken cancellationToken = default)
    {
        return AddAsync(OutboxTargets.Core, "user-registered", "internal/users",
            new { userId, username, email, firstName, lastName }, cancellationToken);
    }

    public Task UserConfirmedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return AddAsync(OutboxTargets.Core, "user-confirmed", $"internal/users/{userId}/confirmed", new { }, cancellationToken);
    }

    public Task SendEmailAsync(string template, string to, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        // The email service uses the outbox id as its message id, so a retried delivery is not sent twice.
        var id = Guid.NewGuid();
        return AddAsync(OutboxTargets.Email, template, "internal/emails",
            new { messageId = id, template, to, data }, cancellationToken, id);
    }

    private Task AddAsync(string target, string type, string path, object body, CancellationToken cancellationToken, Guid? id = null)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return messages.CreateAsync(new OutboxMessage
        {
            Id = id ?? Guid.NewGuid(),
            Target = target,
            Type = type,
            Path = path,
            Body = JsonSerializer.Serialize(body, Json),
            CreatedAt = now,
            NextAttemptAt = now
        }, cancellationToken);
    }
}
