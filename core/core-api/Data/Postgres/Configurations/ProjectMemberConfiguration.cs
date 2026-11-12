using CoreApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreApi.Data.Postgres.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        // One row per user and project: a user has a single role in a project.
        builder.HasKey(m => new { m.UserId, m.ProjectId });

        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => m.ProjectRoleId);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(m => m.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // NO ACTION (checked at the end of the statement) so deleting a project can cascade to its roles and members together.
        builder.HasOne(m => m.Role)
            .WithMany(r => r.Members)
            .HasForeignKey(m => new { m.ProjectId, m.ProjectRoleId })
            .HasPrincipalKey(r => new { r.ProjectId, r.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
