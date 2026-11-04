using CoreApi.Common.Results;
using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using CoreApi.Services.Users;

namespace CoreApi.Services.ProjectMembers;

public class ProjectMemberService(
    IProjectRepository projects,
    IProjectRoleRepository roles,
    IProjectMemberRepository members,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IProjectMemberService
{
    public async Task<Result<PagedResult<ProjectMemberResponse>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var projectError = await CheckProjectAsync(projectId, cancellationToken);
        if (projectError is not null)
        {
            return projectError;
        }

        var result = await members.GetPagedByProjectAsync(projectId, page, pageSize, cancellationToken);
        var items = result.Items.Select(m => m.ToResponse()).ToList();

        return Result.Success(new PagedResult<ProjectMemberResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    public async Task<Result<ProjectMemberResponse>> GrantAsync(Guid projectId, Guid grantedByUserId, GrantProjectAccessRequest request, CancellationToken cancellationToken = default)
    {
        var projectError = await CheckProjectAsync(projectId, cancellationToken);
        if (projectError is not null)
        {
            return projectError;
        }

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return UserErrors.NotFound;
        }

        var role = await roles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.ProjectId != projectId)
        {
            return ProjectRoleErrors.NotFound;
        }

        if (await members.GetAsync(projectId, user.Id, cancellationToken) is not null)
        {
            return ProjectMemberErrors.AlreadyMember;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var member = new ProjectMember
        {
            UserId = user.Id,
            ProjectId = projectId,
            ProjectRoleId = role.Id,
            GrantedByUserId = grantedByUserId,
            GrantedAt = now,
            UpdatedAt = now,
            User = user,
            Role = role
        };

        await members.CreateAsync(member, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(member.ToResponse());
    }

    public async Task<Result<ProjectMemberResponse>> ChangeRoleAsync(Guid projectId, Guid userId, ChangeProjectRoleRequest request, CancellationToken cancellationToken = default)
    {
        var member = await members.GetAsync(projectId, userId, cancellationToken);
        if (member is null)
        {
            return ProjectMemberErrors.NotFound;
        }

        var role = await roles.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.ProjectId != projectId)
        {
            return ProjectRoleErrors.NotFound;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        member.ProjectRoleId = role.Id;
        member.Role = role;
        member.User = user;
        member.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(member.ToResponse());
    }

    public async Task<Result> RevokeAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await members.GetAsync(projectId, userId, cancellationToken);
        if (member is null)
        {
            return ProjectMemberErrors.NotFound;
        }

        await members.DeleteAsync(member, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MyProjectResponse>>> GetMyProjectsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var memberships = await members.GetByUserAsync(userId, cancellationToken);
        IReadOnlyList<MyProjectResponse> items = memberships
            .Where(m => m.Project.IsActive)
            .Select(m => m.ToMyProjectResponse())
            .ToList();

        return Result.Success(items);
    }

    /// <summary>The project must exist and use project tokens; a pass-through project has no members.</summary>
    private async Task<Error?> CheckProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectErrors.NotFound;
        }

        return project.TokenMode == ProjectTokenMode.PassThrough ? ProjectErrors.NoMemberships : null;
    }
}
