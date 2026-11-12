using EmailApi.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmailApi.Data.Postgres;

public interface ISentEmailRepository
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>Records a sent email. Returns false when the message id was recorded meanwhile (a concurrent duplicate).</summary>
    Task<bool> TryAddAsync(SentEmail email, CancellationToken cancellationToken = default);
}

public class SentEmailRepository(PostgresDbContext context) : ISentEmailRepository
{
    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return context.SentEmails.AnyAsync(e => e.MessageId == messageId, cancellationToken);
    }

    public async Task<bool> TryAddAsync(SentEmail email, CancellationToken cancellationToken = default)
    {
        context.SentEmails.Add(email);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(email).State = EntityState.Detached;
            return false;
        }
    }
}
