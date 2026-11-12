using Microsoft.EntityFrameworkCore;

namespace Gateway.Data;

/// <summary>
/// Read-only view of the tables it needs: projects and memberships from the core API (schema <c>core</c>) and the
/// active flag of accounts from identity (schema <c>identity</c>). The gateway never writes and never migrates; in
/// production connect with a database user that only has SELECT on these tables. Only the columns the gateway needs
/// are mapped, so the owners can add columns freely.
/// </summary>
public class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<ProjectRow> Projects => Set<ProjectRow>();
    public DbSet<MemberRow> ProjectMembers => Set<MemberRow>();
    public DbSet<UserRow> Users => Set<UserRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectRow>().ToTable("Projects", "core").HasKey(p => p.Id);
        modelBuilder.Entity<MemberRow>().ToTable("ProjectMembers", "core").HasKey(m => new { m.UserId, m.ProjectId });
        modelBuilder.Entity<UserRow>().ToTable("Users", "identity").HasKey(u => u.Id);
    }
}

public class ProjectRow
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;

    /// <summary>Where API calls are forwarded (<c>/api/{slug}</c>). Null: the project has no backend.</summary>
    public string? BackendUrl { get; set; }

    /// <summary>Internal address of the static frontend, served at <c>/apps/{slug}/</c>. Null: no frontend.</summary>
    public string? FrontendUrl { get; set; }

    public string? HealthUrl { get; set; }

    /// <summary><c>Exchange</c> or <c>PassThrough</c> (stored as text by the core API).</summary>
    public string TokenMode { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MemberRow
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectRoleId { get; set; }
}

public class UserRow
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}
