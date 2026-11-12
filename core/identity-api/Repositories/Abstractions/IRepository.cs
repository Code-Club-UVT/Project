using PlatformKit.Results;

namespace Identity.Repositories.Abstractions;

/// <summary>
/// Basic data access for one entity type. Implementations are per data store.
/// Relational implementations do not persist changes; the caller commits through <see cref="IUnitOfWork"/>.
/// </summary>
public interface IRepository<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    Task<PagedResult<TEntity>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
