using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Catalog;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>
    /// Fixed ids so the catalogue is seeded through a migration and ends up
    /// identical in every environment - a freshly cloned repo has products to
    /// order without any manual setup step.
    /// </summary>
    private static readonly Product[] Catalogue =
    [
        new(Guid.Parse("8f3a1b20-0001-4a00-9c00-000000000001"), "Notebook Pro 14", 7499.00m),
        new(Guid.Parse("8f3a1b20-0002-4a00-9c00-000000000002"), "Monitor 27\" 4K", 2890.00m),
        new(Guid.Parse("8f3a1b20-0003-4a00-9c00-000000000003"), "Teclado mecânico", 649.90m),
        new(Guid.Parse("8f3a1b20-0004-4a00-9c00-000000000004"), "Mouse sem fio", 199.90m),
        new(Guid.Parse("8f3a1b20-0005-4a00-9c00-000000000005"), "Headset com microfone", 450.50m),
        new(Guid.Parse("8f3a1b20-0006-4a00-9c00-000000000006"), "SSD NVMe 2TB", 980.00m),
        new(Guid.Parse("8f3a1b20-0007-4a00-9c00-000000000007"), "Webcam Full HD", 320.00m),
        new(Guid.Parse("8f3a1b20-0008-4a00-9c00-000000000008"), "Cadeira ergonômica", 1250.90m),
        new(Guid.Parse("8f3a1b20-0009-4a00-9c00-000000000009"), "Dock USB-C", 699.00m),
        new(Guid.Parse("8f3a1b20-000a-4a00-9c00-00000000000a"), "Impressora laser", 2340.00m),
    ];

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasColumnType("numeric(18,2)");

        builder.HasIndex(p => p.Name).IsUnique();

        builder.HasData(Catalogue);
    }
}
