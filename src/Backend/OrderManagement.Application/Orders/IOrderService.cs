using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string customer, string product, decimal amount, CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(CancellationToken ct = default);
    Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default);
}
