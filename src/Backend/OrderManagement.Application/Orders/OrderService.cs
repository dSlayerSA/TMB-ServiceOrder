using System.Text.Json;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Application.Orders;

public class OrderService : IOrderService
{
    private const string OrderCreatedEventType = "OrderCreated";

    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(IOrderRepository orderRepository, IOutboxWriter outboxWriter, IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task<Order> CreateOrderAsync(string customer, string product, decimal amount, CancellationToken ct = default)
    {
        var order = Order.Create(customer, product, amount);

        var @event = new OrderCreatedEvent(order.Id, order.Customer, order.Product, order.Amount, order.CreatedAt);
        var payload = JsonSerializer.Serialize(@event);
        var outboxMessage = new OutboxMessage(OrderCreatedEventType, order.Id, payload);

        // Order (+ its status history) and the OutboxMessage are persisted by the
        // same SaveChangesAsync call, which EF Core wraps in a single implicit
        // transaction: either both are committed, or neither is. This is what
        // makes the publication "transactional" (the Outbox Pattern) even though
        // RabbitMQ is never touched directly here.
        _orderRepository.Add(order);
        _outboxWriter.Enqueue(outboxMessage);
        await _unitOfWork.SaveChangesAsync(ct);

        return order;
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default) =>
        await _orderRepository.GetAllAsync(ct);

    public async Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _orderRepository.GetByIdWithHistoryAsync(id, ct) ?? throw new OrderNotFoundException(id);
}
