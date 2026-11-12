using PlatformKit.Results;
using Identity.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres.Repositories;

public class PostgresRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    protected const int MaxPageSize = 100;

    private readonly string _keyName;

    protected DbSet<TEntity> Set { get; }

    public PostgresRepository(PostgresDbContext context)
    {
        Set = context.Set<TEntity>();
        _keyName = context.Model.FindEntityType(typeof(TEntity))!.FindPrimaryKey()!.Properties[0].Name;
    }

    public async Task<PagedResult<TEntity>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await Set.CountAsync(cancellationToken);
        var items = await Set
            .AsNoTracking()
            .OrderBy(e => EF.Property<TKey>(e, _keyName))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, page, pageSize, totalCount);
    }

    public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Set.FindAsync([id], cancellationToken);
    }

    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        return Task.CompletedTask;
    }
}
