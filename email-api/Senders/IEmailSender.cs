using EmailApi.Templates;

namespace EmailApi.Senders;

/// <summary>Delivers a rendered email. Throws when delivery fails, so the caller's retry kicks in.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, EmailContent content, CancellationToken cancellationToken = default);
}
