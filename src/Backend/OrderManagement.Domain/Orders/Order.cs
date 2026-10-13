using OrderManagement.Domain.Catalog;

namespace OrderManagement.Domain.Orders;

public class Order
{
    /// <summary>
    /// Pendente -> Processando -> Finalizado is the mandatory happy path, and it
    /// is still the only route to Finalizado. Falhou and Cancelado are alternative
    /// terminal states that end an order early; nothing leaves them.
    /// </summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pendente] = [OrderStatus.Processando, OrderStatus.Cancelado],
        [OrderStatus.Processando] = [OrderStatus.Finalizado, OrderStatus.Falhou, OrderStatus.Cancelado],
        [OrderStatus.Finalizado] = [],
        [OrderStatus.Falhou] = [],
        [OrderStatus.Cancelado] = []
    };

    private readonly List<OrderStatusHistory> _statusHistories = new();

    public Guid Id { get; private set; }
    public string Customer { get; private set; } = string.Empty;

    /// <summary>
    /// Catalogue item this order was placed from. The name and amount below are
    /// a snapshot taken at that moment, so a later price change does not alter
    /// what this order was worth.
    /// </summary>
    public Guid? ProductId { get; private set; }

    public string Product { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public OrderStatus Status { get; private set; }

    /// <summary>How long the worker should take on this order, and how it should end.</summary>
    public ProcessingPlan Plan { get; private set; } = ProcessingPlan.Default;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<OrderStatusHistory> StatusHistories => _statusHistories;

    public bool IsTerminal => AllowedTransitions[Status].Length == 0;

    private Order() { }

    public static Order Create(string customer, Product product, ProcessingPlan? plan = null)
    {
        if (string.IsNullOrWhiteSpace(customer))
            throw new ArgumentException("Customer is required.", nameof(customer));
        ArgumentNullException.ThrowIfNull(product);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Customer = customer,
            ProductId = product.Id,
            Product = product.Name,
            Amount = product.Price,
            Status = OrderStatus.Pendente,
            Plan = plan ?? ProcessingPlan.Default,
            CreatedAt = now,
            UpdatedAt = now
        };
        order._statusHistories.Add(new OrderStatusHistory(order.Id, OrderStatus.Pendente, "Pedido criado."));
        return order;
    }

    /// <summary>
    /// Moves the order to the given status, following <see cref="AllowedTransitions"/>.
    /// Idempotent: a request to transition to the current status is a no-op
    /// (covers message redelivery / reprocessing).
    /// </summary>
    /// <returns>
    /// The newly created history entry if the transition was applied, or null if the
    /// order was already in the target status (no-op).
    /// </returns>
    public OrderStatusHistory? TransitionTo(OrderStatus target, string? note = null)
    {
        if (Status == target)
            return null;

        if (!AllowedTransitions[Status].Contains(target))
            throw new InvalidOperationException(
                $"Invalid status transition for order {Id}: cannot go from {Status} to {target}.");

        Status = target;
        UpdatedAt = DateTime.UtcNow;
        var history = new OrderStatusHistory(Id, target, note);
        _statusHistories.Add(history);
        return history;
    }
}
