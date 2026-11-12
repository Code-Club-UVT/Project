using CoreApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreApi.Data.Postgres.Configurations;

public class UserTabConfiguration : IEntityTypeConfiguration<UserTab>
{
    public const int MaxLastPathLength = 500;

    public void Configure(EntityTypeBuilder<UserTab> builder)
    {
        // One tab per user and app.
        builder.HasKey(t => new { t.UserId, t.ProjectId });

        builder.Property(t => t.LastPath).HasMaxLength(MaxLastPathLength).IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Position });

        // The tab only makes sense while the user is a member: revoking access or deleting the project removes it.
        builder.HasOne(t => t.Member)
            .WithMany()
            .HasForeignKey(t => new { t.UserId, t.ProjectId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
