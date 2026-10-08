using OrderManagement.Domain.Orders;

namespace OrderManagement.Api.Dtos;

public record OrderResponse(
    Guid Id,
    string Customer,
    string Product,
    decimal Amount,
    string Status,
    DateTime CreatedAt)
{
    public static OrderResponse FromDomain(Order order) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.Status.ToString(),
        order.CreatedAt);
}
