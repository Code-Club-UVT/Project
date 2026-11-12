namespace Identity.Services.Outbox;

/// <summary>
/// Queues calls to other services. They are added to the current unit of work, so they are saved by the same
/// <c>SaveChangesAsync</c> as the change that caused them (or not at all), and delivered afterwards by <c>OutboxDispatcher</c>.
/// </summary>
public interface IOutbox
{
    /// <summary>Tells the core API about a new account, so it creates the profile (name, read copy of username and email).</summary>
    Task UserRegisteredAsync(Guid userId, string username, string email, string? firstName, string? lastName, CancellationToken cancellationToken = default);

    /// <summary>Tells the core API the email is confirmed, so it grants the default roles of the projects.</summary>
    Task UserConfirmedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Asks the email service to send a template, for example <c>email-confirmation</c> with a link.</summary>
    Task SendEmailAsync(string template, string to, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default);
}

public static class OutboxTargets
{
    public const string Core = "core";
    public const string Email = "email";
}

public static class EmailTemplates
{
    public const string EmailConfirmation = "email-confirmation";
    public const string PasswordReset = "password-reset";
    public const string LinkKey = "link";
}
