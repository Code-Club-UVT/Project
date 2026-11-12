namespace Identity.Entities;

/// <summary>
/// A call to another service, saved in the same transaction as the change that caused it and delivered later by
/// <c>OutboxDispatcher</c>, with retries. So a user is never saved without the core API and the email service
/// eventually hearing about it, even when they are down at that moment.
/// </summary>
public class OutboxMessage
{
    /// <summary>Also sent to the receiver, so it can recognise a retried delivery (the email service uses it as the message id).</summary>
    public Guid Id { get; set; }

    /// <summary>Which service: <c>core</c> or <c>email</c> (see <c>OutboxTargets</c>).</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>What happened, for logs and inspection, for example <c>user-registered</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The relative path POSTed to on the target, for example <c>internal/users</c>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The JSON body. Can hold a raw emailed token (inside a link), so rows are deleted once delivered.</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int Attempts { get; set; }

    /// <summary>When the next delivery may be tried. Also works as a lease while a dispatcher is delivering it.</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>Set when delivery stopped after the maximum number of attempts; the row is kept for inspection.</summary>
    public DateTime? FailedAt { get; set; }

    public string? LastError { get; set; }
}
