using Identity.Entities;
using Identity.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres.Repositories;

public class PostgresRefreshTokenRepository(PostgresDbContext context)
    : PostgresRepository<RefreshToken, Guid>(context), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return Set
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        return await Set.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Set.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);
    }
}
