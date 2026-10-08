using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Order?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(CancellationToken ct = default);
}
