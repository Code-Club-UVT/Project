using EmailApi.Templates;

namespace EmailApi.Senders;

/// <summary>
/// Development-only sender: writes the plain-text body (which holds the link, and so a raw token) to the log instead of
/// sending it. It must never be registered outside Development.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV EMAIL] To {Email}, subject '{Subject}':\n{Text}", to, content.Subject, content.Text);
        return Task.CompletedTask;
    }
}
