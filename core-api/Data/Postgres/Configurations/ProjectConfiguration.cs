using CoreApi.Entities;
using CoreApi.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreApi.Data.Postgres.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.BackendUrl).HasMaxLength(500);
        builder.Property(p => p.FrontendUrl).HasMaxLength(500);
        builder.Property(p => p.HealthUrl).HasMaxLength(500);
        builder.Property(p => p.TokenMode).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ProjectTokenMode.Exchange);
        builder.Property(p => p.IsActive).HasDefaultValue(true);

        builder.HasIndex(p => p.Slug).IsUnique();

        // The slug is part of a URL path and a token audience.
        builder.ToTable(t => t.HasCheckConstraint("CK_Projects_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"));

        builder.HasMany(p => p.Roles)
            .WithOne(r => r.Project)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Plain FK (the service checks the role belongs to this project). Deleting the role clears the default.
        builder.HasOne<ProjectRole>()
            .WithMany()
            .HasForeignKey(p => p.DefaultRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Only a system project may forward the edge token unchanged (identity).
        builder.ToTable(t => t.HasCheckConstraint("CK_Projects_PassThrough_System", "\"TokenMode\" <> 'PassThrough' OR \"IsSystem\""));

        builder.HasMany(p => p.Members)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
