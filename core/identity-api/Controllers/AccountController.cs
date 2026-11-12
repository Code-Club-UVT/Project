using Identity.Extensions;
using Identity.Models.Requests;
using Identity.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

/// <summary>Account actions of the logged-in user (edge token required).</summary>
[Authorize]
public class AccountController(IAccountService accountService) : ApiControllerBase
{
    /// <summary>Checks the current password, sets the new one and ends every session (all refresh tokens are revoked).</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken));
    }
}
