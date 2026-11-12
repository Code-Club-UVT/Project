using PlatformKit.Results;
using Identity.Models.Requests;
using Identity.Models.Responses;

namespace Identity.Services.Auth;

public interface IAccountService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>Always succeeds, so it does not reveal whether the email is registered.</summary>
    Task<Result> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Always succeeds, so it does not reveal whether the email is registered.</summary>
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
