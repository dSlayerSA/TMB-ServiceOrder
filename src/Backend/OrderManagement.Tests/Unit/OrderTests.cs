using FluentAssertions;
using OrderManagement.Domain.Catalog;
using OrderManagement.Domain.Orders;
using Xunit;

namespace OrderManagement.Tests.Unit;

public class OrderTests
{
    private static Product Notebook() =>
        new(Guid.NewGuid(), "Notebook Pro 14", 7499.00m);

    [Fact]
    public void Create_StartsAsPendenteWithInitialHistoryEntry()
    {
        var order = Order.Create("Maria Silva", Notebook());

        order.Status.Should().Be(OrderStatus.Pendente);
        order.StatusHistories.Should().ContainSingle(h => h.Status == OrderStatus.Pendente);
    }

    [Fact]
    public void Create_SnapshotsNameAndPriceFromTheProduct()
    {
        var product = Notebook();

        var order = Order.Create("Maria Silva", product);

        // The snapshot is what keeps a later repricing from rewriting this order.
        order.ProductId.Should().Be(product.Id);
        order.Product.Should().Be(product.Name);
        order.Amount.Should().Be(product.Price);
    }

    [Fact]
    public void Create_RejectsBlankCustomer()
    {
        var act = () => Order.Create("  ", Notebook());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransitionTo_FollowsMandatorySequence()
    {
        var order = Order.Create("Maria Silva", Notebook());

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
        var order = Order.Create("Maria Silva", Notebook());
        order.TransitionTo(OrderStatus.Processando);

        var result = order.TransitionTo(OrderStatus.Processando);

        result.Should().BeNull();
        order.Status.Should().Be(OrderStatus.Processando);
        order.StatusHistories.Should().HaveCount(2); // Pendente + Processando, no duplicate
    }

    [Fact]
    public void TransitionTo_CannotSkipOrGoBackwards()
    {
        var order = Order.Create("Maria Silva", Notebook());

        var skipAct = () => order.TransitionTo(OrderStatus.Finalizado);
        skipAct.Should().Throw<InvalidOperationException>();

        order.TransitionTo(OrderStatus.Processando);
        order.TransitionTo(OrderStatus.Finalizado);

        var backwardsAct = () => order.TransitionTo(OrderStatus.Processando);
        backwardsAct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TransitionTo_AllowsFailingFromProcessando()
    {
        var order = Order.Create("Maria Silva", Notebook());
        order.TransitionTo(OrderStatus.Processando);

        order.TransitionTo(OrderStatus.Falhou, "deu ruim").Should().NotBeNull();

        order.Status.Should().Be(OrderStatus.Falhou);
        order.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void TransitionTo_AllowsCancellingFromPendenteOrProcessando()
    {
        var fromPending = Order.Create("Maria Silva", Notebook());
        fromPending.TransitionTo(OrderStatus.Cancelado).Should().NotBeNull();
        fromPending.Status.Should().Be(OrderStatus.Cancelado);

        var fromProcessing = Order.Create("Maria Silva", Notebook());
        fromProcessing.TransitionTo(OrderStatus.Processando);
        fromProcessing.TransitionTo(OrderStatus.Cancelado).Should().NotBeNull();
        fromProcessing.Status.Should().Be(OrderStatus.Cancelado);
    }

    [Theory]
    [InlineData(OrderStatus.Falhou)]
    [InlineData(OrderStatus.Cancelado)]
    public void TransitionTo_TerminalStatesAreADeadEnd(OrderStatus terminal)
    {
        var order = Order.Create("Maria Silva", Notebook());
        order.TransitionTo(OrderStatus.Processando);
        order.TransitionTo(terminal);

        // Nothing leaves a terminal state - not even back to the happy path.
        var act = () => order.TransitionTo(OrderStatus.Finalizado);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TransitionTo_CannotFailAnOrderThatNeverStarted()
    {
        var order = Order.Create("Maria Silva", Notebook());

        var act = () => order.TransitionTo(OrderStatus.Falhou);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_UsesTheDefaultPlanWhenNoneIsGiven()
    {
        var order = Order.Create("Maria Silva", Notebook());

        order.Plan.DurationSeconds.Should().Be(ProcessingPlan.DefaultDurationSeconds);
        order.Plan.Outcome.Should().Be(ProcessingOutcome.Concluir);
        order.Plan.FinalStatus.Should().Be(OrderStatus.Finalizado);
    }

    [Theory]
    [InlineData(ProcessingOutcome.Concluir, OrderStatus.Finalizado)]
    [InlineData(ProcessingOutcome.Falhar, OrderStatus.Falhou)]
    [InlineData(ProcessingOutcome.Cancelar, OrderStatus.Cancelado)]
    public void Plan_MapsOutcomeToTheTerminalStatus(ProcessingOutcome outcome, OrderStatus expected)
    {
        new ProcessingPlan(10, outcome).FinalStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ProcessingPlan.MaxDurationSeconds + 1)]
    public void Plan_RejectsDurationsOutsideTheAllowedWindow(int seconds)
    {
        var act = () => new ProcessingPlan(seconds, ProcessingOutcome.Concluir);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public class ProductTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_RejectsNonPositivePrice(decimal price)
    {
        var act = () => new Product(Guid.NewGuid(), "Notebook", price);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RejectsBlankName()
    {
        var act = () => new Product(Guid.NewGuid(), "   ", 10m);
        act.Should().Throw<ArgumentException>();
    }
}
