using OrderManagement.Domain.Catalog;

namespace OrderManagement.Application.Catalog;

/// <summary>
/// Read access to the product catalogue. Orders are always placed against a
/// product that already exists here, so the price is never supplied by the
/// caller and cannot be tampered with from the outside.
/// </summary>
public interface IProductCatalog
{
    Task<List<Product>> GetAllAsync(CancellationToken ct = default);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(Guid id)
        : base($"Product {id} was not found.")
    {
        ProductId = id;
    }

    public Guid ProductId { get; }
}
