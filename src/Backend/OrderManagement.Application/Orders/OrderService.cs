using System.Diagnostics;
using System.Text.Json;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Catalog;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Application.Orders;

public class OrderService : IOrderService
{
    private const string OrderCreatedEventType = "OrderCreated";

    private readonly IOrderRepository _orderRepository;
    private readonly IProductCatalog _productCatalog;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orderRepository,
        IProductCatalog productCatalog,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productCatalog = productCatalog;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task<Order> CreateOrderAsync(
        string customer,
        Guid productId,
        ProcessingPlan? plan = null,
        CancellationToken ct = default)
    {
        // The price comes from the catalogue, never from the request, so a
        // client cannot decide what its own order is worth.
        var product = await _productCatalog.GetByIdAsync(productId, ct)
            ?? throw new ProductNotFoundException(productId);

        var order = Order.Create(customer, product, plan);

        var @event = new OrderCreatedEvent(order.Id, order.Customer, order.Product, order.Amount, order.CreatedAt);
        var payload = JsonSerializer.Serialize(@event);
        // Activity.Current?.Id is the W3C traceparent of the incoming request;
        // it rides along on the row so the eventual publish stays in this trace.
        var outboxMessage = new OutboxMessage(OrderCreatedEventType, order.Id, payload, Activity.Current?.Id);

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

    public Task<PagedResult<Order>> SearchAsync(OrderQuery query, CancellationToken ct = default) =>
        _orderRepository.SearchAsync(query.Normalized(), ct);

    public async Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _orderRepository.GetByIdWithHistoryAsync(id, ct) ?? throw new OrderNotFoundException(id);
}
