namespace OrderManagement.Domain.Orders;

public class OrderStatusHistory
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? Note { get; private set; }

    private OrderStatusHistory() { }

    public OrderStatusHistory(Guid orderId, OrderStatus status, string? note = null)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Status = status;
        ChangedAt = DateTime.UtcNow;
        Note = note;
    }
}
