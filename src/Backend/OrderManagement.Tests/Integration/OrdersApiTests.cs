using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Api.Dtos;
using OrderManagement.Infrastructure.Persistence;
using Xunit;

namespace OrderManagement.Tests.Integration;

public class OrdersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostOrders_PersistsOrderHistoryAndOutboxMessageAtomically()
    {
        var request = new CreateOrderRequest("Maria Silva", "Notebook", 3500m);

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();
        created.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();

        var order = await db.Orders.Include(o => o.StatusHistories).FirstOrDefaultAsync(o => o.Id == created!.Id);
        order.Should().NotBeNull();
        order!.Status.ToString().Should().Be("Pendente");
        order.StatusHistories.Should().ContainSingle();

        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.CorrelationId == created!.Id);
        outboxMessage.Should().NotBeNull();
        outboxMessage!.EventType.Should().Be("OrderCreated");
        outboxMessage.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderById_Returns404WhenOrderDoesNotExist()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_ReturnsCreatedOrder()
    {
        var request = new CreateOrderRequest("Joao Souza", "Mouse", 99.90m);
        await _client.PostAsJsonAsync("/orders", request);

        var response = await _client.GetAsync("/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.Should().Contain(o => o.Customer == "Joao Souza" && o.Product == "Mouse");
    }
}
