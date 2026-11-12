using Identity.Entities;
using Identity.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres.Repositories;

public class PostgresUserRepository(PostgresDbContext context)
    : PostgresRepository<User, Guid>(context), IUserRepository
{
    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return identifier.Contains('@')
            ? Set.FirstOrDefaultAsync(u => u.Email == identifier, cancellationToken)
            : Set.FirstOrDefaultAsync(u => u.Username == identifier, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return Set.AnyAsync(u => u.Username == username, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return Set.AnyAsync(u => u.Email == email, cancellationToken);
    }
}
