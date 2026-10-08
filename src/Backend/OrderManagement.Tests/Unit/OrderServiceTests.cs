using FluentAssertions;
using Moq;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;
using Xunit;

namespace OrderManagement.Tests.Unit;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOutboxWriter> _outboxWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(_orderRepository.Object, _outboxWriter.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderAndEnqueuesOutboxMessageInSameUnitOfWork()
    {
        Order? addedOrder = null;
        OutboxMessage? enqueuedMessage = null;

        _orderRepository.Setup(r => r.Add(It.IsAny<Order>())).Callback<Order>(o => addedOrder = o);
        _outboxWriter.Setup(w => w.Enqueue(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(m => enqueuedMessage = m);

        var order = await _sut.CreateOrderAsync("Maria Silva", "Notebook", 3500m);

        addedOrder.Should().BeSameAs(order);
        order.Status.Should().Be(OrderStatus.Pendente);

        enqueuedMessage.Should().NotBeNull();
        enqueuedMessage!.EventType.Should().Be("OrderCreated");
        enqueuedMessage.CorrelationId.Should().Be(order.Id);
        enqueuedMessage.Payload.Should().Contain(order.Id.ToString());

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
