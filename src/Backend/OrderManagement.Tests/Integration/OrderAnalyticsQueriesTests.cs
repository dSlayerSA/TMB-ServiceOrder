using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Analytics;
using OrderManagement.Domain.Catalog;
using OrderManagement.Domain.Orders;
using OrderManagement.Infrastructure.Persistence;
using Xunit;

namespace OrderManagement.Tests.Integration;

/// <summary>
/// Exercises the analytics aggregates against a real PostgreSQL instance - the
/// grouping, the sums and the history-derived processing time are SQL behaviour,
/// so an in-memory provider would prove very little here.
///
/// Each test starts from an empty Orders table so the assertions can be exact
/// counts rather than "greater than".
/// </summary>
public class OrderAnalyticsQueriesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderAnalyticsQueriesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Forces the host (and its migrations) to be built before the first scope.
        factory.CreateClient();
    }

    [Fact]
    public async Task CountAsync_FiltersByStatus()
    {
        await ResetAsync();
        await SeedAsync(
            NewOrder("Maria", "Notebook", 3500m, OrderStatus.Pendente),
            NewOrder("Joao", "Mouse", 100m, OrderStatus.Processando),
            NewOrder("Ana", "Teclado", 250m, OrderStatus.Finalizado),
            NewOrder("Bia", "Monitor", 900m, OrderStatus.Finalizado));

        var queries = Queries(out var scope);
        using (scope)
        {
            (await queries.CountAsync(new OrderAnalyticsFilter())).Should().Be(4);
            (await queries.CountAsync(new OrderAnalyticsFilter(OrderStatus.Finalizado))).Should().Be(2);
            (await queries.CountAsync(new OrderAnalyticsFilter(OrderStatus.Pendente))).Should().Be(1);
        }
    }

    [Fact]
    public async Task SumAmountsAsync_SumsOnlyMatchingOrders()
    {
        await ResetAsync();
        await SeedAsync(
            NewOrder("Maria", "Notebook", 3500m, OrderStatus.Finalizado),
            NewOrder("Joao", "Mouse", 99.90m, OrderStatus.Finalizado),
            NewOrder("Ana", "Teclado", 250m, OrderStatus.Pendente));

        var queries = Queries(out var scope);
        using (scope)
        {
            var finalized = await queries.SumAmountsAsync(new OrderAnalyticsFilter(OrderStatus.Finalizado));
            finalized.Count.Should().Be(2);
            finalized.TotalAmount.Should().Be(3599.90m);

            var everything = await queries.SumAmountsAsync(new OrderAnalyticsFilter());
            everything.TotalAmount.Should().Be(3849.90m);
        }
    }

    [Fact]
    public async Task SumAmountsAsync_ReturnsZeroWhenNothingMatches()
    {
        await ResetAsync();

        var queries = Queries(out var scope);
        using (scope)
        {
            var result = await queries.SumAmountsAsync(new OrderAnalyticsFilter(OrderStatus.Finalizado));

            result.Count.Should().Be(0);
            result.TotalAmount.Should().Be(0m);
        }
    }

    [Fact]
    public async Task BreakdownByStatusAsync_GroupsCountsAndTotals()
    {
        await ResetAsync();
        await SeedAsync(
            NewOrder("Maria", "Notebook", 3500m, OrderStatus.Pendente),
            NewOrder("Joao", "Mouse", 100m, OrderStatus.Pendente),
            NewOrder("Ana", "Teclado", 250m, OrderStatus.Finalizado));

        var queries = Queries(out var scope);
        using (scope)
        {
            var breakdown = await queries.BreakdownByStatusAsync(new OrderAnalyticsFilter());

            breakdown.Should().HaveCount(2);
            breakdown.Single(b => b.Status == "Pendente").Count.Should().Be(2);
            breakdown.Single(b => b.Status == "Pendente").TotalAmount.Should().Be(3600m);
            breakdown.Single(b => b.Status == "Finalizado").Count.Should().Be(1);
        }
    }

    [Fact]
    public async Task ProcessingTimeAsync_ConsidersOnlyFinalizedOrders()
    {
        await ResetAsync();
        await SeedAsync(
            NewOrder("Maria", "Notebook", 3500m, OrderStatus.Finalizado),
            NewOrder("Joao", "Mouse", 100m, OrderStatus.Processando),
            NewOrder("Ana", "Teclado", 250m, OrderStatus.Pendente));

        var queries = Queries(out var scope);
        using (scope)
        {
            var result = await queries.ProcessingTimeAsync(new OrderAnalyticsFilter());

            // Only the finalized order has a Pendente -> Finalizado span to measure.
            result.SampleSize.Should().Be(1);
            result.AverageSeconds.Should().NotBeNull().And.BeGreaterThanOrEqualTo(0);
            result.FastestSeconds.Should().Be(result.SlowestSeconds);
        }
    }

    [Fact]
    public async Task ProcessingTimeAsync_ReportsNoSamplesWhenNothingIsFinalized()
    {
        await ResetAsync();
        await SeedAsync(NewOrder("Maria", "Notebook", 3500m, OrderStatus.Pendente));

        var queries = Queries(out var scope);
        using (scope)
        {
            var result = await queries.ProcessingTimeAsync(new OrderAnalyticsFilter());

            result.SampleSize.Should().Be(0);
            result.AverageSeconds.Should().BeNull();
        }
    }

    [Fact]
    public async Task CreatedToUtc_IsAnExclusiveUpperBound()
    {
        await ResetAsync();
        await SeedAsync(NewOrder("Maria", "Notebook", 3500m, OrderStatus.Pendente));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IOrderAnalyticsQueries>();
        var createdAt = await db.Orders.Select(o => o.CreatedAt).SingleAsync();

        // Upper bound equal to the order's own timestamp must exclude it, which is
        // what lets a caller express "this month" as [1st, 1st of next month).
        (await queries.CountAsync(new OrderAnalyticsFilter(CreatedToUtc: createdAt)))
            .Should().Be(0);

        (await queries.CountAsync(new OrderAnalyticsFilter(CreatedFromUtc: createdAt)))
            .Should().Be(1);
    }

    [Fact]
    public async Task ListOrdersAsync_CapsTheRequestedLimit()
    {
        await ResetAsync();
        await SeedAsync(Enumerable.Range(0, 5)
            .Select(i => NewOrder($"Cliente {i}", "Produto", 10m, OrderStatus.Pendente))
            .ToArray());

        var queries = Queries(out var scope);
        using (scope)
        {
            (await queries.ListOrdersAsync(new OrderAnalyticsFilter(), limit: 2)).Should().HaveCount(2);
            // Out-of-range limits are clamped, never passed through to the database.
            (await queries.ListOrdersAsync(new OrderAnalyticsFilter(), limit: 999)).Should().HaveCount(5);
            (await queries.ListOrdersAsync(new OrderAnalyticsFilter(), limit: 0)).Should().HaveCount(1);
        }
    }

    /// <summary>
    /// Each seeded order gets its own catalogue product, which is what carries
    /// the amount now that orders are priced from the catalogue.
    /// </summary>
    private static (Product Product, Order Order) NewOrder(
        string customer,
        string product,
        decimal amount,
        OrderStatus target)
    {
        var catalogueItem = new Product(Guid.NewGuid(), $"{product} {Guid.NewGuid():N}", amount);
        var order = Order.Create(customer, catalogueItem);

        if (target is OrderStatus.Processando or OrderStatus.Finalizado)
            order.TransitionTo(OrderStatus.Processando);
        if (target is OrderStatus.Finalizado)
            order.TransitionTo(OrderStatus.Finalizado);

        return (catalogueItem, order);
    }

    private IOrderAnalyticsQueries Queries(out IServiceScope scope)
    {
        scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOrderAnalyticsQueries>();
    }

    private async Task SeedAsync(params (Product Product, Order Order)[] seeded)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
        db.Products.AddRange(seeded.Select(s => s.Product));
        db.Orders.AddRange(seeded.Select(s => s.Order));
        await db.SaveChangesAsync();
    }

    private async Task ResetAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"OrderStatusHistories\", \"Orders\", \"Products\" CASCADE;");
    }
}
