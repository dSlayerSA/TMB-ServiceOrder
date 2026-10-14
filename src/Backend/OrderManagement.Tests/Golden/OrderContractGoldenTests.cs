using Moq;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Catalog;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Catalog;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Tests.Golden;

/// <summary>
/// Golden tests for the two contracts this system exposes to the outside world:
/// the HTTP response bodies (consumed by the frontend) and the broker payloads
/// (consumed by the Worker). Both are serialized differently on purpose - the
/// Api uses ASP.NET Core's camelCase defaults, while the event payloads are
/// written with the raw serializer defaults (PascalCase) - and that asymmetry
/// is exactly the kind of thing these snapshots keep honest.
/// </summary>
public class OrderContractGoldenTests
{
    private static readonly Product Notebook =
        new(Guid.Parse("8f3a1b20-0001-4a00-9c00-000000000001"), "Notebook Pro 14", 7499.00m);

    private static Order FinalizedOrder()
    {
        var order = Order.Create("Maria Silva", Notebook);
        order.TransitionTo(OrderStatus.Processando, "Processamento iniciado pelo worker.");
        order.TransitionTo(OrderStatus.Finalizado, "Pedido finalizado pelo worker.");
        return order;
    }

    [Fact]
    public void OrderResponse_HttpContractIsStable()
    {
        var response = OrderResponse.FromDomain(Order.Create("Maria Silva", Notebook));

        JsonGolden.Verify("order-response", response, JsonGolden.WebContract);
    }

    [Fact]
    public void OrderDetailResponse_HttpContractIsStable()
    {
        var response = OrderDetailResponse.FromDomain(FinalizedOrder());

        JsonGolden.Verify("order-detail-response", response, JsonGolden.WebContract);
    }

    /// <summary>
    /// Captures the payload produced by the real <see cref="OrderService"/>, so the
    /// snapshot reflects the bytes that actually reach the broker rather than a
    /// re-implementation of the serialization.
    /// </summary>
    [Fact]
    public async Task OrderCreatedEvent_MessagingContractIsStable()
    {
        OutboxMessage? enqueued = null;
        var repository = new Mock<IOrderRepository>();
        var catalog = new Mock<IProductCatalog>();
        var outboxWriter = new Mock<IOutboxWriter>();
        var unitOfWork = new Mock<IUnitOfWork>();
        catalog.Setup(c => c.GetByIdAsync(Notebook.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Notebook);
        outboxWriter.Setup(w => w.Enqueue(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(m => enqueued = m);

        var service = new OrderService(
            repository.Object, catalog.Object, outboxWriter.Object, unitOfWork.Object,
            new Mock<IOrderStatusNotifier>().Object);
        await service.CreateOrderAsync("Maria Silva", Notebook.Id);

        Assert.NotNull(enqueued);
        Assert.Equal("OrderCreated", enqueued!.EventType);
        JsonGolden.VerifyJson("order-created-event-payload", enqueued.Payload);
    }

    [Fact]
    public void OrderStatusChangedEvent_MessagingContractIsStable()
    {
        var @event = new OrderStatusChangedEvent(Guid.NewGuid(), OrderStatus.Processando, DateTime.UtcNow);

        JsonGolden.Verify("order-status-changed-event-payload", @event, JsonGolden.MessagingContract);
    }
}
