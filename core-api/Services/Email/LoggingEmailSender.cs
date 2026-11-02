namespace CoreApi.Services.Email;

/// <summary>
/// Development-only sender: writes the email link to the log instead of sending it.
/// The link contains a raw token, so this must never be registered outside Development.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendEmailConfirmationAsync(string email, string link, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV EMAIL] Confirm email for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV EMAIL] Password reset for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }
}
