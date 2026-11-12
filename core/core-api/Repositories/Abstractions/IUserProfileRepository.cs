using CoreApi.Entities;
using PlatformKit.Results;

namespace CoreApi.Repositories.Abstractions;

public interface IUserProfileRepository : IRepository<UserProfile, Guid>
{
    /// <summary>Paged list ordered by username.</summary>
    Task<PagedResult<UserProfile>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
