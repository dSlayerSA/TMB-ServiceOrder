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
    private readonly Mock<IOrderStatusNotifier> _statusNotifier = new();
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
            _unitOfWork.Object,
            _statusNotifier.Object);
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

    [Fact]
    public async Task CancelAsync_CancelsAnOrderTheWorkerHasNotPickedUpYet()
    {
        var order = Order.Create("Maria Silva", _notebook);
        _orderRepository
            .Setup(r => r.GetByIdWithHistoryAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var cancelled = await _sut.CancelAsync(order.Id);

        cancelled.Status.Should().Be(OrderStatus.Cancelado);
        cancelled.StatusHistories.Should().Contain(h => h.Status == OrderStatus.Cancelado);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // The hub lives in this process, so cancelling pushes to it directly
        // instead of going out over the broker and back.
        _statusNotifier.Verify(
            n => n.NotifyStatusChangedAsync(order.Id, OrderStatus.Cancelado, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_RefusesAnOrderThatHasAlreadySettled()
    {
        var order = Order.Create("Maria Silva", _notebook);
        order.TransitionTo(OrderStatus.Processando);
        order.TransitionTo(OrderStatus.Finalizado);

        _orderRepository
            .Setup(r => r.GetByIdWithHistoryAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var cancelling = async () => await _sut.CancelAsync(order.Id);

        await cancelling.Should().ThrowAsync<OrderNotCancellableException>();
        order.Status.Should().Be(OrderStatus.Finalizado);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_ThrowsWhenTheOrderDoesNotExist()
    {
        _orderRepository
            .Setup(r => r.GetByIdWithHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var cancelling = async () => await _sut.CancelAsync(Guid.NewGuid());

        await cancelling.Should().ThrowAsync<OrderNotFoundException>();
    }
}
