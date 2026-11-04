using CoreApi.Enums;
using CoreApi.Extensions;
using CoreApi.Models.Requests;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

/// <summary>Admin management of the APIs behind the gateway, their roles and who can use them.</summary>
[Authorize(Roles = nameof(UserRole.Admin))]
public class ProjectsController(
    IProjectService projectService,
    IProjectRoleService roleService,
    IProjectMemberService memberService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return ToActionResult(await projectService.GetPagedAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectService.GetByIdAsync(projectId, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectService.UpdateAsync(projectId, request, cancellationToken));
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> DeleteProject(Guid projectId, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectService.DeleteAsync(projectId, cancellationToken));
    }

    [HttpGet("{projectId:guid}/roles")]
    public async Task<IActionResult> GetRoles(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return ToActionResult(await roleService.GetPagedAsync(projectId, page, pageSize, cancellationToken));
    }

    [HttpPost("{projectId:guid}/roles")]
    public async Task<IActionResult> CreateRole(Guid projectId, ProjectRoleRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await roleService.CreateAsync(projectId, request, cancellationToken));
    }

    [HttpPut("{projectId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid projectId, Guid roleId, ProjectRoleRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await roleService.UpdateAsync(projectId, roleId, request, cancellationToken));
    }

    [HttpDelete("{projectId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid projectId, Guid roleId, CancellationToken cancellationToken)
    {
        return ToActionResult(await roleService.DeleteAsync(projectId, roleId, cancellationToken));
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return ToActionResult(await memberService.GetPagedAsync(projectId, page, pageSize, cancellationToken));
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> GrantAccess(Guid projectId, GrantProjectAccessRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await memberService.GrantAsync(projectId, User.GetUserId(), request, cancellationToken));
    }

    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> ChangeMemberRole(Guid projectId, Guid userId, ChangeProjectRoleRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await memberService.ChangeRoleAsync(projectId, userId, request, cancellationToken));
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RevokeAccess(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        return ToActionResult(await memberService.RevokeAsync(projectId, userId, cancellationToken));
    }
}
