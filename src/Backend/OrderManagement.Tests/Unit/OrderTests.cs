using FluentAssertions;
using OrderManagement.Domain.Orders;
using Xunit;

namespace OrderManagement.Tests.Unit;

public class OrderTests
{
    [Fact]
    public void Create_StartsAsPendenteWithInitialHistoryEntry()
    {
        var order = Order.Create("Maria Silva", "Notebook", 3500m);

        order.Status.Should().Be(OrderStatus.Pendente);
        order.StatusHistories.Should().ContainSingle(h => h.Status == OrderStatus.Pendente);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_RejectsNonPositiveAmount(decimal amount)
    {
        var act = () => Order.Create("Maria Silva", "Notebook", amount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransitionTo_FollowsMandatorySequence()
    {
        var order = Order.Create("Maria Silva", "Notebook", 3500m);

        var toProcessing = order.TransitionTo(OrderStatus.Processando);
        toProcessing.Should().NotBeNull();
        order.Status.Should().Be(OrderStatus.Processando);

        var toFinished = order.TransitionTo(OrderStatus.Finalizado);
        toFinished.Should().NotBeNull();
        order.Status.Should().Be(OrderStatus.Finalizado);

        order.StatusHistories.Select(h => h.Status).Should().BeEquivalentTo(
            new[] { OrderStatus.Pendente, OrderStatus.Processando, OrderStatus.Finalizado },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void TransitionTo_RepeatingTheCurrentStatusIsANoOp()
    {
        var order = Order.Create("Maria Silva", "Notebook", 3500m);
        order.TransitionTo(OrderStatus.Processando);

        var result = order.TransitionTo(OrderStatus.Processando);

        result.Should().BeNull();
        order.Status.Should().Be(OrderStatus.Processando);
        order.StatusHistories.Should().HaveCount(2); // Pendente + Processando, no duplicate
    }

    [Fact]
    public void TransitionTo_CannotSkipOrGoBackwards()
    {
        var order = Order.Create("Maria Silva", "Notebook", 3500m);

        var skipAct = () => order.TransitionTo(OrderStatus.Finalizado);
        skipAct.Should().Throw<InvalidOperationException>();

        order.TransitionTo(OrderStatus.Processando);
        order.TransitionTo(OrderStatus.Finalizado);

        var backwardsAct = () => order.TransitionTo(OrderStatus.Processando);
        backwardsAct.Should().Throw<InvalidOperationException>();
    }
}
