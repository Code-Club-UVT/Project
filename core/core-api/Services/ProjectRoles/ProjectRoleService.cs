using PlatformKit.Results;
using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Projects;

namespace CoreApi.Services.ProjectRoles;

public class ProjectRoleService(
    IProjectRepository projects,
    IProjectRoleRepository roles,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IProjectRoleService
{
    public async Task<Result<PagedResult<ProjectRoleResponse>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var projectError = await CheckProjectAsync(projectId, cancellationToken);
        if (projectError is not null)
        {
            return projectError;
        }

        var result = await roles.GetPagedByProjectAsync(projectId, page, pageSize, cancellationToken);
        var items = result.Items.Select(r => r.ToResponse()).ToList();

        return Result.Success(new PagedResult<ProjectRoleResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    public async Task<Result<ProjectRoleResponse>> CreateAsync(Guid projectId, ProjectRoleRequest request, CancellationToken cancellationToken = default)
    {
        var projectError = await CheckProjectAsync(projectId, cancellationToken);
        if (projectError is not null)
        {
            return projectError;
        }

        var name = request.Name.Trim();
        if (await roles.GetByNameAsync(projectId, name, cancellationToken) is not null)
        {
            return ProjectRoleErrors.NameTaken;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var role = new ProjectRole
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Description = NullIfBlank(request.Description),
            CreatedAt = now,
            UpdatedAt = now
        };

        await roles.CreateAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(role.ToResponse());
    }

    public async Task<Result<ProjectRoleResponse>> UpdateAsync(Guid projectId, Guid roleId, ProjectRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null || role.ProjectId != projectId)
        {
            return ProjectRoleErrors.NotFound;
        }

        if (await IsCoreAdminRoleAsync(role, cancellationToken))
        {
            return ProjectRoleErrors.SystemRole;
        }

        var name = request.Name.Trim();
        var sameName = await roles.GetByNameAsync(projectId, name, cancellationToken);
        if (sameName is not null && sameName.Id != role.Id)
        {
            return ProjectRoleErrors.NameTaken;
        }

        role.Name = name;
        role.Description = NullIfBlank(request.Description);
        role.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(role.ToResponse());
    }

    public async Task<Result> DeleteAsync(Guid projectId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null || role.ProjectId != projectId)
        {
            return ProjectRoleErrors.NotFound;
        }

        if (await IsCoreAdminRoleAsync(role, cancellationToken))
        {
            return ProjectRoleErrors.SystemRole;
        }

        // A project's default role may be deleted: the default is cleared (ON DELETE SET NULL).
        if (await roles.HasMembersAsync(roleId, cancellationToken))
        {
            return ProjectRoleErrors.InUse;
        }

        await roles.DeleteAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<bool> IsCoreAdminRoleAsync(ProjectRole role, CancellationToken cancellationToken)
    {
        if (!string.Equals(role.Name, CoreRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var project = await projects.GetByIdAsync(role.ProjectId, cancellationToken);
        return project is { IsSystem: true, Slug: ProjectService.CoreSlug };
    }

    /// <summary>The project must exist and use project tokens; a pass-through project has no roles.</summary>
    private async Task<Error?> CheckProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectErrors.NotFound;
        }

        return project.TokenMode == ProjectTokenMode.PassThrough ? ProjectErrors.NoMemberships : null;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
