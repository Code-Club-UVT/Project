using PlatformKit.Results;
using CoreApi.Entities;

namespace CoreApi.Repositories.Abstractions;

public interface IProjectRoleRepository : IRepository<ProjectRole, Guid>
{
    Task<PagedResult<ProjectRole>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProjectRole?> GetByNameAsync(Guid projectId, string name, CancellationToken cancellationToken = default);
    Task<bool> HasMembersAsync(Guid roleId, CancellationToken cancellationToken = default);
}
