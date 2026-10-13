using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(
        string customer,
        Guid productId,
        ProcessingPlan? plan = null,
        CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(CancellationToken ct = default);

    Task<PagedResult<Order>> SearchAsync(OrderQuery query, CancellationToken ct = default);
    Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default);
}
