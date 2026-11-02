namespace CoreApi.Services.Email;

public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string email, string link, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string email, string link, CancellationToken cancellationToken = default);
}
