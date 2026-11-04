using CoreApi.Common.Results;
using CoreApi.Entities;

namespace CoreApi.Repositories.Abstractions;

public interface IProjectRepository : IRepository<Project, Guid>
{
    Task<Project?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Paged list ordered by slug (unlike <c>GetAllAsync</c>, which orders by id).</summary>
    Task<PagedResult<Project>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
