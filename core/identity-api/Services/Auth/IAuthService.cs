using PlatformKit.Results;
using Identity.Models.Requests;

namespace Identity.Services.Auth;

public interface IAuthService
{
    Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a refresh token for a new session and rotates the token.</summary>
    Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes the given refresh token. Always succeeds, so logging out twice is harmless.</summary>
    Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
}
