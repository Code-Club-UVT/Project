using Identity.Entities;
using Identity.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres.Repositories;

public class PostgresOutboxRepository(PostgresDbContext context)
    : PostgresRepository<OutboxMessage, Guid>(context), IOutboxRepository
{
    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTime now, DateTime leaseUntil, int batchSize, CancellationToken cancellationToken = default)
    {
        // One statement, so the claim is atomic and committed immediately (no transaction is held while delivering).
        var claimed = await Set.FromSqlInterpolated($"""
                UPDATE identity."OutboxMessages" SET "NextAttemptAt" = {leaseUntil}
                WHERE "Id" IN (
                    SELECT "Id" FROM identity."OutboxMessages"
                    WHERE "FailedAt" IS NULL AND "NextAttemptAt" <= {now}
                    ORDER BY "CreatedAt", "Id"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // RETURNING does not keep the order; deliver oldest first (a user's "registered" before "confirmed").
        return claimed.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
    }
}
