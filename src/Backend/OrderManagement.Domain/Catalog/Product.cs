namespace OrderManagement.Domain.Catalog;

/// <summary>
/// A product in the catalogue, the source of a new order's name and price.
/// Orders keep their own copy of both (see <see cref="Orders.Order"/>), so
/// repricing a product never rewrites the history of orders already placed.
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    private Product() { }

    public Product(Guid id, string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero.", nameof(price));

        Id = id;
        Name = name;
        Price = price;
    }
}
