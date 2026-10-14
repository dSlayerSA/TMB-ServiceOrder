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

        // The id is assigned by the domain constructor, not by the store. Without
        // this, EF treats a new history found through Order.StatusHistories as an
        // existing row (its key is already set) and issues an UPDATE that matches
        // nothing - which surfaces as a DbUpdateConcurrencyException on save.
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.ChangedAt);
        builder.Property(h => h.Note).HasMaxLength(500);

        builder.HasIndex(h => h.OrderId);
    }
}
