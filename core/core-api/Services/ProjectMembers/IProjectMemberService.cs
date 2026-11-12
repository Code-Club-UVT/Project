using PlatformKit.Results;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;

namespace CoreApi.Services.ProjectMembers;

public interface IProjectMemberService
{
    Task<Result<PagedResult<ProjectMemberResponse>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ProjectMemberResponse>> GrantAsync(Guid projectId, Guid grantedByUserId, GrantProjectAccessRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProjectMemberResponse>> ChangeRoleAsync(Guid projectId, Guid userId, ChangeProjectRoleRequest request, CancellationToken cancellationToken = default);
    Task<Result> RevokeAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The projects the user can open (used by the SPA for navigation).</summary>
    Task<Result<IReadOnlyList<MyProjectResponse>>> GetMyProjectsAsync(Guid userId, CancellationToken cancellationToken = default);
}
