using Microsoft.EntityFrameworkCore;

namespace Identity.Data.CoreRead;

/// <summary>
/// Read-only view of the core API's project tables (schema <c>core</c>), needed to mint project tokens. Identity never
/// writes here and never migrates it; in production its database user has SELECT only on these three tables. Only
/// the needed columns are mapped, so the core API can add columns freely.
/// </summary>
public class CoreReadDbContext(DbContextOptions<CoreReadDbContext> options) : DbContext(options)
{
    public const string Schema = "core";

    public DbSet<ProjectRow> Projects => Set<ProjectRow>();
    public DbSet<ProjectRoleRow> ProjectRoles => Set<ProjectRoleRow>();
    public DbSet<ProjectMemberRow> ProjectMembers => Set<ProjectMemberRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ProjectRow>().ToTable("Projects").HasKey(p => p.Id);
        modelBuilder.Entity<ProjectRoleRow>().ToTable("ProjectRoles").HasKey(r => r.Id);
        modelBuilder.Entity<ProjectMemberRow>().ToTable("ProjectMembers").HasKey(m => new { m.UserId, m.ProjectId });
    }
}

public class ProjectRow
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;

    /// <summary><c>Exchange</c> or <c>PassThrough</c> (stored as text by the core API).</summary>
    public string TokenMode { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class ProjectRoleRow
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProjectMemberRow
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectRoleId { get; set; }
}
