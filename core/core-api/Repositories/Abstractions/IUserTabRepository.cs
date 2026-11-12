using CoreApi.Entities;

namespace CoreApi.Repositories.Abstractions;

/// <summary>Not an <see cref="IRepository{TEntity,TKey}"/>: the key is two columns (user and project).</summary>
public interface IUserTabRepository
{
    /// <summary>The user's tabs ordered by position, with their project loaded (read only).</summary>
    Task<IReadOnlyList<UserTab>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's tabs as tracked entities, to be updated or removed before saving.</summary>
    Task<IReadOnlyList<UserTab>> GetTrackedByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task CreateAsync(UserTab tab, CancellationToken cancellationToken = default);
    Task DeleteAsync(UserTab tab, CancellationToken cancellationToken = default);
}
