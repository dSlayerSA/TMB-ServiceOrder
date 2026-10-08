using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Customer).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Product).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Amount).HasColumnType("numeric(18,2)");
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.CreatedAt);
        builder.Property(o => o.UpdatedAt);

        builder.Metadata.FindNavigation(nameof(Order.StatusHistories))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(o => o.StatusHistories)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
