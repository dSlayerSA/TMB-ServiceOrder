using OrderManagement.Application.Orders;

namespace OrderManagement.Application.Abstractions;

/// <summary>
/// Publishes order status change events to the broker so other processes
/// (notably the Api, which hosts the real-time hub) can react to them.
/// </summary>
public interface IOrderStatusEventPublisher
{
    Task PublishAsync(OrderStatusChangedEvent @event, CancellationToken ct = default);
}
