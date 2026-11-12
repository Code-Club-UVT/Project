namespace Identity.Repositories.Abstractions;

/// <summary>Commits the pending changes of the relational (PostgreSQL) repositories in one transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
