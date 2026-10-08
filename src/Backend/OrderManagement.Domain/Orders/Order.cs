namespace OrderManagement.Domain.Orders;

public class Order
{
    private static readonly Dictionary<OrderStatus, OrderStatus> NextStatus = new()
    {
        [OrderStatus.Pendente] = OrderStatus.Processando,
        [OrderStatus.Processando] = OrderStatus.Finalizado
    };

    private readonly List<OrderStatusHistory> _statusHistories = new();

    public Guid Id { get; private set; }
    public string Customer { get; private set; } = string.Empty;
    public string Product { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<OrderStatusHistory> StatusHistories => _statusHistories;

    private Order() { }

    public static Order Create(string customer, string product, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(customer))
            throw new ArgumentException("Customer is required.", nameof(customer));
        if (string.IsNullOrWhiteSpace(product))
            throw new ArgumentException("Product is required.", nameof(product));
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            Product = product,
            Amount = amount,
            Status = OrderStatus.Pendente,
            CreatedAt = now,
            UpdatedAt = now
        };
        order._statusHistories.Add(new OrderStatusHistory(order.Id, OrderStatus.Pendente, "Pedido criado."));
        return order;
    }

    /// <summary>
    /// Advances the order to the given status following the mandatory sequence
    /// Pendente -> Processando -> Finalizado. Idempotent: a request to transition
    /// to the current status is a no-op (covers message redelivery / reprocessing).
    /// </summary>
    /// <returns>
    /// The newly created history entry if the transition was applied, or null if the
    /// order was already in the target status (no-op).
    /// </returns>
    public OrderStatusHistory? TransitionTo(OrderStatus target, string? note = null)
    {
        if (Status == target)
            return null;

        if (!NextStatus.TryGetValue(Status, out var expectedNext) || expectedNext != target)
            throw new InvalidOperationException(
                $"Invalid status transition for order {Id}: cannot go from {Status} to {target}.");

        Status = target;
        UpdatedAt = DateTime.UtcNow;
        var history = new OrderStatusHistory(Id, target, note);
        _statusHistories.Add(history);
        return history;
    }
}
