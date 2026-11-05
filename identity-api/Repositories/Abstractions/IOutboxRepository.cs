using Identity.Entities;

namespace Identity.Repositories.Abstractions;

public interface IOutboxRepository : IRepository<OutboxMessage, Guid>
{
    /// <summary>
    /// Takes up to <paramref name="batchSize"/> due messages (oldest first) and moves their next attempt to
    /// <paramref name="leaseUntil"/>, committed at once. Rows locked by another dispatcher are skipped, so several
    /// instances never deliver the same message at the same time.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTime now, DateTime leaseUntil, int batchSize, CancellationToken cancellationToken = default);
}
