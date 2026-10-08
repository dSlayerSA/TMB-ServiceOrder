namespace OrderManagement.Application.Orders;

/// <summary>
/// Event contract published to the messaging broker when an order is created.
/// Serialized as the Outbox message payload and as the AMQP message body.
/// </summary>
public record OrderCreatedEvent(
    Guid OrderId,
    string Customer,
    string Product,
    decimal Amount,
    DateTime CreatedAt);
