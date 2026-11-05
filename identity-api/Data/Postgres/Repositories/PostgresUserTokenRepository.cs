using Identity.Entities;
using Identity.Enums;
using Identity.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres.Repositories;

public class PostgresUserTokenRepository(PostgresDbContext context)
    : PostgresRepository<UserToken, Guid>(context), IUserTokenRepository
{
    public Task<UserToken?> GetByHashAsync(string tokenHash, UserTokenType type, CancellationToken cancellationToken = default)
    {
        return Set
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Type == type, cancellationToken);
    }

    public async Task<IReadOnlyList<UserToken>> GetUnusedAsync(Guid userId, UserTokenType type, CancellationToken cancellationToken = default)
    {
        return await Set.Where(t => t.UserId == userId && t.Type == type && t.UsedAt == null).ToListAsync(cancellationToken);
    }
}
