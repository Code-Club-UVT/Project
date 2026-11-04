using CoreApi.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CoreApi.Services.Email;

/// <summary>
/// Sends the emails over SMTP with MailKit. A new connection is opened per message, which is fine for
/// the low volume of account emails. Failures are logged (without the link, which holds a raw token)
/// and rethrown, so the request fails with a 500 from <c>GlobalExceptionHandler</c>.
/// </summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _smtp = options.Value;

    public Task SendEmailConfirmationAsync(string email, string link, CancellationToken cancellationToken = default) =>
        SendAsync(email, EmailTemplates.EmailConfirmation(_smtp.FromName, link), cancellationToken);

    public Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken = default) =>
        SendAsync(email, EmailTemplates.PasswordReset(_smtp.FromName, link), cancellationToken);

    private async Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = content.Subject;
        message.Body = new BodyBuilder { TextBody = content.Text, HtmlBody = content.Html }.ToMessageBody();

        try
        {
            using var client = new SmtpClient { Timeout = _smtp.TimeoutSeconds * 1000 };
            await client.ConnectAsync(
                _smtp.Host,
                _smtp.Port,
                _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect,
                cancellationToken);
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send email '{Subject}' to {Email} through {Host}:{Port}", content.Subject, to, _smtp.Host, _smtp.Port);
            throw;
        }

        logger.LogInformation("Email '{Subject}' sent to {Email}", content.Subject, to);
    }
}
