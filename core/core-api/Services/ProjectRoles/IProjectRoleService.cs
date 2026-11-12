using PlatformKit.Results;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;

namespace CoreApi.Services.ProjectRoles;

public interface IProjectRoleService
{
    Task<Result<PagedResult<ProjectRoleResponse>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ProjectRoleResponse>> CreateAsync(Guid projectId, ProjectRoleRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProjectRoleResponse>> UpdateAsync(Guid projectId, Guid roleId, ProjectRoleRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid projectId, Guid roleId, CancellationToken cancellationToken = default);
}
