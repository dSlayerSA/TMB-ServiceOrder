using OrderManagement.Domain.Catalog;

namespace OrderManagement.Api.Dtos;

public record ProductResponse(Guid Id, string Name, decimal Price)
{
    public static ProductResponse FromDomain(Product product) =>
        new(product.Id, product.Name, product.Price);
}
