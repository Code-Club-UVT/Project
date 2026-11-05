using PlatformKit.Results;
using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresProjectRepository(PostgresDbContext context)
    : PostgresRepository<Project, Guid>(context), IProjectRepository
{
    public Task<Project?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return Set.FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        return Set.AnyAsync(p => p.Slug == slug, cancellationToken);
    }

    public async Task<PagedResult<Project>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await Set.CountAsync(cancellationToken);
        var items = await Set
            .AsNoTracking()
            .OrderBy(p => p.Slug)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Project>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<Project>> GetActiveWithDefaultRoleAsync(CancellationToken cancellationToken = default)
    {
        return await Set.Where(p => p.IsActive && p.DefaultRoleId != null).ToListAsync(cancellationToken);
    }
}
