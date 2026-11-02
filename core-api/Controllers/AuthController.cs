using CoreApi.Common.Results;
using CoreApi.Extensions;
using CoreApi.Models.Requests;
using CoreApi.Options;
using CoreApi.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CoreApi.Controllers;

[AllowAnonymous]
[EnableRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy)]
public class AuthController(
    IAuthService authService,
    IAccountService accountService,
    IOptions<RefreshTokenOptions> refreshOptions) : ApiControllerBase
{
    // The cookie is only sent to the auth endpoints.
    private const string RefreshCookiePath = "/api/auth";

    private readonly RefreshTokenOptions _refresh = refreshOptions.Value;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.RegisterAsync(request, cancellationToken));
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ConfirmEmailAsync(request, cancellationToken));
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ResendConfirmationAsync(request, cancellationToken));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return ToSessionResult(await authService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(Request.Cookies[_refresh.CookieName], cancellationToken);
        if (result.IsFailure)
        {
            DeleteRefreshCookie();
        }

        return ToSessionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(Request.Cookies[_refresh.CookieName], cancellationToken);
        DeleteRefreshCookie();

        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ForgotPasswordAsync(request, cancellationToken));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ResetPasswordAsync(request, cancellationToken));
    }

    private IActionResult ToSessionResult(Result<AuthSession> result)
    {
        if (result.IsFailure)
        {
            return ToActionResult(result);
        }

        var session = result.Value;
        Response.Cookies.Append(_refresh.CookieName, session.RefreshToken, CreateCookieOptions(session.RefreshTokenExpiresAt));

        return Ok(session.Response);
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(_refresh.CookieName, CreateCookieOptions(expiresAt: null));
    }

    private CookieOptions CreateCookieOptions(DateTime? expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _refresh.CookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            IsEssential = true,
            Expires = expiresAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc))
        };
    }
}
