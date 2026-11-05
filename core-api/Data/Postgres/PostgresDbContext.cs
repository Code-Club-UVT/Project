using CoreApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres;

/// <summary>The core API's tables, all in the <c>core</c> schema (migrations history included).</summary>
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public const string Schema = "core";

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgresDbContext).Assembly);
    }
}
