using EmailApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailApi.Data.Postgres;

/// <summary>The email service's own tables, all in the <c>email</c> schema (migrations history included).</summary>
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public const string Schema = "email";

    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgresDbContext).Assembly);
    }
}
