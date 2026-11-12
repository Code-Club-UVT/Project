using Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Data.Postgres;

/// <summary>Identity's own tables, all in the <c>identity</c> schema (migrations history included).</summary>
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgresDbContext).Assembly, t => t.Namespace == typeof(Configurations.UserConfiguration).Namespace);
    }
}
