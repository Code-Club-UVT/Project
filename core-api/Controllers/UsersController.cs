using CoreApi.Extensions;
using CoreApi.Models.Requests;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.Projects;
using CoreApi.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

[Authorize]
public class UsersController(IUserService userService, IProjectMemberService memberService) : ApiControllerBase
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

    [HttpGet("me/projects")]
    public async Task<IActionResult> GetMyProjects(CancellationToken cancellationToken)
    {
        return ToActionResult(await memberService.GetMyProjectsAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet]
    [Authorize(Roles = CoreRoles.Admin)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return ToActionResult(await userService.GetPagedAsync(page, pageSize, cancellationToken));
    }
}
