using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Error).HasMaxLength(1000);
        // "00-<32 hex trace id>-<16 hex span id>-<2 hex flags>" is 55 chars.
        builder.Property(m => m.TraceParent).HasMaxLength(64);

        builder.HasIndex(m => m.ProcessedAt);
    }
}
