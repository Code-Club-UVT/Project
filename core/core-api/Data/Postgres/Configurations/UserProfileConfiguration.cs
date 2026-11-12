using CoreApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreApi.Data.Postgres.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();

        builder.Property(p => p.Username).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(256).IsRequired();
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100);

        // Not unique: identity owns uniqueness, and a rename seen out of order must not fail here.
        builder.HasIndex(p => p.Username);
    }
}
