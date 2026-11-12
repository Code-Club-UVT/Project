using Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Data.Postgres.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Target).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Type).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Path).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Body).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1000);

        // The dispatcher's query: pending messages that are due, oldest first.
        builder.HasIndex(m => m.NextAttemptAt).HasFilter("\"FailedAt\" IS NULL");
    }
}
