using FluentAssertions;
using Moq;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Catalog;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Catalog;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;
using Xunit;

namespace OrderManagement.Tests.Unit;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IProductCatalog> _productCatalog = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OrderService _sut;

    private readonly Product _notebook = new(Guid.NewGuid(), "Notebook Pro 14", 7499.00m);

    public OrderServiceTests()
    {
        _productCatalog
            .Setup(c => c.GetByIdAsync(_notebook.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_notebook);

        _sut = new OrderService(
            _orderRepository.Object,
            _productCatalog.Object,
            _outboxWriter.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderAndEnqueuesOutboxMessageInSameUnitOfWork()
    {
        Order? addedOrder = null;
        OutboxMessage? enqueuedMessage = null;

        _orderRepository.Setup(r => r.Add(It.IsAny<Order>())).Callback<Order>(o => addedOrder = o);
        _outboxWriter.Setup(w => w.Enqueue(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(m => enqueuedMessage = m);

        var order = await _sut.CreateOrderAsync("Maria Silva", _notebook.Id);

        addedOrder.Should().BeSameAs(order);
        order.Status.Should().Be(OrderStatus.Pendente);

        enqueuedMessage.Should().NotBeNull();
        enqueuedMessage!.EventType.Should().Be("OrderCreated");
        enqueuedMessage.CorrelationId.Should().Be(order.Id);
        enqueuedMessage.Payload.Should().Contain(order.Id.ToString());

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_TakesTheAmountFromTheCatalogue()
    {
        var order = await _sut.CreateOrderAsync("Maria Silva", _notebook.Id);

        order.Amount.Should().Be(_notebook.Price);
        order.Product.Should().Be(_notebook.Name);
        order.ProductId.Should().Be(_notebook.Id);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenTheProductDoesNotExist()
    {
        _productCatalog
            .Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => _sut.CreateOrderAsync("Maria Silva", Guid.NewGuid());

        await act.Should().ThrowAsync<ProductNotFoundException>();
        // Nothing may be written when the order cannot be built.
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsWhenOrderDoesNotExist()
    {
        _orderRepository.Setup(r => r.GetByIdWithHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var act = () => _sut.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }
}
