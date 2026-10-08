using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.ChangedAt);
        builder.Property(h => h.Note).HasMaxLength(500);

        builder.HasIndex(h => h.OrderId);
    }
}
