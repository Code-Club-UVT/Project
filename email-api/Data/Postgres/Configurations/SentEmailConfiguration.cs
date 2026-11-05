using EmailApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailApi.Data.Postgres.Configurations;

public class SentEmailConfiguration : IEntityTypeConfiguration<SentEmail>
{
    public void Configure(EntityTypeBuilder<SentEmail> builder)
    {
        builder.HasKey(e => e.MessageId);
        builder.Property(e => e.MessageId).ValueGeneratedNever();
        builder.Property(e => e.Template).HasMaxLength(100).IsRequired();
        builder.Property(e => e.To).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Caller).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.SentAt);
    }
}
