using CoreApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreApi.Data.Postgres.Configurations;

public class ProjectRoleConfiguration : IEntityTypeConfiguration<ProjectRole>
{
    public void Configure(EntityTypeBuilder<ProjectRole> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasColumnType("citext").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasIndex(r => new { r.ProjectId, r.Name }).IsUnique();

        // Lets ProjectMember point at (ProjectId, Id), so a member can only hold a role of the same project.
        builder.HasAlternateKey(r => new { r.ProjectId, r.Id });
    }
}
