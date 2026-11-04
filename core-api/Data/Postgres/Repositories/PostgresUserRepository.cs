using CoreApi.Common.Results;
using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresUserRepository(PostgresDbContext context)
    : PostgresRepository<User, Guid>(context), IUserRepository
{
    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var users = Set.Include(u => u.Profile);

        return identifier.Contains('@')
            ? users.FirstOrDefaultAsync(u => u.Email == identifier, cancellationToken)
            : users.FirstOrDefaultAsync(u => u.Username == identifier, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Set.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return Set.AnyAsync(u => u.Username == username, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return Set.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<PagedResult<User>> GetPagedWithProfileAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await Set.CountAsync(cancellationToken);
        var items = await Set
            .AsNoTracking()
            .Include(u => u.Profile)
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, page, pageSize, totalCount);
    }
}
