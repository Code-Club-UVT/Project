using Identity.Repositories.Abstractions;

namespace Identity.Data.Postgres.Repositories;

public class PostgresUnitOfWork(PostgresDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
