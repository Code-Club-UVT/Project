using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresUserTabRepository(PostgresDbContext context) : IUserTabRepository
{
    private DbSet<UserTab> Set => context.UserTabs;

    public async Task<IReadOnlyList<UserTab>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(t => t.Member).ThenInclude(m => m.Project)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserTab>> GetTrackedByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Set.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(UserTab tab, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(tab, cancellationToken);
    }

    public Task DeleteAsync(UserTab tab, CancellationToken cancellationToken = default)
    {
        Set.Remove(tab);
        return Task.CompletedTask;
    }
}
