using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Catalog;
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
        // Restrict: a product that orders point at must not be deletable out
        // from under them.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(o => o.CreatedAt);
        builder.Property(o => o.UpdatedAt);

        // Stored inline on Orders: the plan has no identity of its own and is
        // always read with the order it belongs to.
        builder.OwnsOne(o => o.Plan, plan =>
        {
            plan.Property(p => p.DurationSeconds).HasColumnName("ProcessingDurationSeconds");
            plan.Property(p => p.Outcome)
                .HasColumnName("ProcessingOutcome")
                .HasConversion<string>()
                .HasMaxLength(20);
        });
        builder.Navigation(o => o.Plan).IsRequired();

        builder.Metadata.FindNavigation(nameof(Order.StatusHistories))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(o => o.StatusHistories)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
