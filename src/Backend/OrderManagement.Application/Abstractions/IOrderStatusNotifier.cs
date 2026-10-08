using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Abstractions;

/// <summary>
/// Abstraction over pushing order status changes to connected clients (e.g. via SignalR).
/// </summary>
public interface IOrderStatusNotifier
{
    Task NotifyStatusChangedAsync(Guid orderId, OrderStatus status, CancellationToken ct = default);
}
