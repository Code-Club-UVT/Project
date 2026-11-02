using CoreApi.Enums;
using CoreApi.Extensions;
using CoreApi.Models.Requests;
using CoreApi.Services.Auth;
using CoreApi.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

[Authorize]
public class UsersController(IUserService userService, IAccountService accountService) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        return ToActionResult(await userService.GetByIdAsync(User.GetUserId(), cancellationToken));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await userService.UpdateProfileAsync(User.GetUserId(), request, cancellationToken));
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await accountService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return ToActionResult(await userService.GetPagedAsync(page, pageSize, cancellationToken));
    }
}
