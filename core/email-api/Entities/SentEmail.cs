namespace EmailApi.Entities;

/// <summary>An email that was handed to the mail server. The caller's message id makes a repeated request a no-op.</summary>
public class SentEmail
{
    public Guid MessageId { get; set; }

    public string Template { get; set; } = string.Empty;

    /// <summary>The recipient. Kept for support questions ("did my email go out?"); the body is never stored.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Which service asked for it (the internal caller name).</summary>
    public string Caller { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
}
