using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using PlatformKit.Results;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresUserProfileRepository(PostgresDbContext context)
    : PostgresRepository<UserProfile, Guid>(context), IUserProfileRepository
{
    public async Task<PagedResult<UserProfile>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await Set.CountAsync(cancellationToken);
        var items = await Set
            .AsNoTracking()
            .OrderBy(p => p.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserProfile>(items, page, pageSize, totalCount);
    }
}
