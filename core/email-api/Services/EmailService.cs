using EmailApi.Data.Postgres;
using EmailApi.Entities;
using EmailApi.Models;
using EmailApi.Options;
using EmailApi.Senders;
using EmailApi.Templates;
using Microsoft.Extensions.Options;
using PlatformKit.Results;

namespace EmailApi.Services;

public interface IEmailService
{
    /// <summary>Renders the template and sends it, unless this message id was already sent. Throws when delivery fails.</summary>
    Task<Result> SendAsync(SendEmailRequest request, string caller, CancellationToken cancellationToken = default);
}

/// <summary>
/// Delivery is at least once: the message id is recorded after the mail server accepted the email, so a crash in
/// between can lead to one duplicate when the caller retries. Retries themselves come from the caller (identity's
/// outbox); a failure here is an exception and the caller sees a 500.
/// </summary>
public class EmailService(
    IEnumerable<IEmailTemplate> templates,
    IEmailSender sender,
    ISentEmailRepository sentEmails,
    IOptions<SmtpOptions> smtpOptions,
    TimeProvider timeProvider,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly Dictionary<string, IEmailTemplate> _templates = templates.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public async Task<Result> SendAsync(SendEmailRequest request, string caller, CancellationToken cancellationToken = default)
    {
        if (!_templates.TryGetValue(request.Template, out var template))
        {
            return EmailErrors.UnknownTemplate(request.Template);
        }

        var data = request.Data ?? new Dictionary<string, string>();
        var missing = template.RequiredKeys.Where(k => !data.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        if (missing.Count > 0)
        {
            return EmailErrors.MissingData(template.Name, missing);
        }

        if (template.Validate(data) is { } problem)
        {
            return EmailErrors.InvalidData(problem);
        }

        var messageId = request.MessageId!.Value;
        if (await sentEmails.ExistsAsync(messageId, cancellationToken))
        {
            logger.LogInformation("Email {MessageId} was already sent; skipped", messageId);
            return Result.Success();
        }

        await sender.SendAsync(request.To, template.Render(smtpOptions.Value.FromName, data), cancellationToken);

        var recorded = await sentEmails.TryAddAsync(new SentEmail
        {
            MessageId = messageId,
            Template = template.Name,
            To = request.To,
            Caller = caller,
            SentAt = timeProvider.GetUtcNow().UtcDateTime
        }, cancellationToken);

        if (!recorded)
        {
            logger.LogWarning("Email {MessageId} was sent twice by concurrent requests", messageId);
        }

        return Result.Success();
    }
}
