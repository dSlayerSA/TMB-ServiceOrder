using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

/// <summary>
/// Event published by the Worker whenever an order's status changes, so the
/// Api process (which hosts the SignalR hub) can relay the update to clients.
/// </summary>
public record OrderStatusChangedEvent(Guid OrderId, OrderStatus Status, DateTime ChangedAt);
