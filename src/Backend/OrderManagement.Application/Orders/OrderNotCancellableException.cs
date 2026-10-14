using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

/// <summary>
/// Raised when cancellation is asked for an order that has already settled.
/// Finalizado, Falhou and Cancelado are terminal: nothing leaves them.
/// </summary>
public class OrderNotCancellableException : Exception
{
    public Guid OrderId { get; }
    public OrderStatus Status { get; }

    public OrderNotCancellableException(Guid orderId, OrderStatus status)
        : base($"Order {orderId} cannot be cancelled because it is already {status}.")
    {
        OrderId = orderId;
        Status = status;
    }
}
