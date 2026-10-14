using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Analytics;
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
    public async Task GetProducts_ReturnsTheSeededCatalogue()
    {
        var response = await _client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();

        products.Should().NotBeNullOrEmpty();
        products!.Should().Contain(p => p.Name == Catalogue.Notebook);
        products.Should().OnlyContain(p => p.Price > 0);
    }

    [Fact]
    public async Task PostOrders_PersistsOrderHistoryAndOutboxMessageAtomically()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        var response = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Maria Silva", notebook.Id));

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
    public async Task PostOrders_PricesTheOrderFromTheCatalogue()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        var response = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Maria Silva", notebook.Id));
        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();

        created!.Amount.Should().Be(notebook.Price);
        created.Product.Should().Be(notebook.Name);
    }

    [Fact]
    public async Task PostOrders_RejectsAnUnknownProduct()
    {
        var response = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Maria Silva", Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrders_AcceptsAPlanWithTheOutcomeSentAsAString()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        // Posted as a raw anonymous object on purpose: this is the exact wire
        // shape the frontend sends, and a JSON string enum only binds when the
        // converter is registered.
        var response = await _client.PostAsJsonAsync(
            "/orders",
            new { customer = "Teste Cenario", productId = notebook.Id, processingSeconds = 12, outcome = "Falhar" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();
        created!.ProcessingSeconds.Should().Be(12);
        created.Outcome.Should().Be("Falhar");
    }

    [Fact]
    public async Task PostOrders_RejectsAProcessingTimeOutsideTheAllowedWindow()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        var response = await _client.PostAsJsonAsync(
            "/orders",
            new { customer = "Teste", productId = notebook.Id, processingSeconds = 9999 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrders_DefaultsToTheBriefsBehaviourWhenNoPlanIsSent()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        var response = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Sem plano", notebook.Id));
        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();

        created!.ProcessingSeconds.Should().Be(5);
        created.Outcome.Should().Be("Concluir");
    }

    [Fact]
    public async Task GetOrderById_Returns404WhenOrderDoesNotExist()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(1, 13)]   // 1h in 5-minute buckets
    [InlineData(6, 13)]   // 6h in 30-minute buckets
    [InlineData(24, 25)]  // 24h in hourly buckets
    public async Task GetDashboard_ChangesBucketGranularityWithTheWindow(int hours, int expectedBuckets)
    {
        var snapshot = await _client.GetFromJsonAsync<DashboardResponse>($"/dashboard?hours={hours}");

        snapshot!.Throughput.Should().HaveCount(expectedBuckets);
        snapshot.Throughput.Last().IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task GetDashboard_FallsBackToADayForAnUnsupportedWindow()
    {
        var snapshot = await _client.GetFromJsonAsync<DashboardResponse>("/dashboard?hours=999");

        snapshot!.Throughput.Should().HaveCount(25);
    }

    [Fact]
    public async Task GetDashboard_PutsAJustCreatedOrderInTheCurrentBucket()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);
        await _client.PostAsJsonAsync("/orders", new CreateOrderRequest("Agora", notebook.Id));

        // The finest window must show it immediately, which is the whole point
        // of being able to zoom in.
        var snapshot = await _client.GetFromJsonAsync<DashboardResponse>("/dashboard?hours=1");

        snapshot!.Throughput.Last().Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOrders_ReturnsCreatedOrder()
    {
        var mouse = await Catalogue.GetAsync(_client, Catalogue.Mouse);
        await _client.PostAsJsonAsync("/orders", new CreateOrderRequest("Joao Souza", mouse.Id));

        var response = await _client.GetAsync("/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderResponse>>();
        page.Should().NotBeNull();
        page!.Items.Should().Contain(o => o.Customer == "Joao Souza" && o.Product == mouse.Name);
        page.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOrders_PagesTheResults()
    {
        var mouse = await Catalogue.GetAsync(_client, Catalogue.Mouse);
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/orders", new CreateOrderRequest($"Paginado {i}", mouse.Id));

        var first = await _client.GetFromJsonAsync<PagedResponse<OrderResponse>>("/orders?page=1&pageSize=2");

        first!.Items.Should().HaveCount(2);
        first.PageSize.Should().Be(2);
        first.TotalPages.Should().Be((int)Math.Ceiling(first.Total / 2.0));

        var second = await _client.GetFromJsonAsync<PagedResponse<OrderResponse>>("/orders?page=2&pageSize=2");
        second!.Items.Should().NotBeEmpty();
        // Pages must not overlap, or rows would be shown twice while scrolling.
        second.Items.Select(o => o.Id).Should().NotIntersectWith(first.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task GetOrders_FiltersBySearchTermAndStatus()
    {
        var teclado = await Catalogue.GetAsync(_client, Catalogue.Teclado);
        await _client.PostAsJsonAsync("/orders", new CreateOrderRequest("Zulmira Buscavel", teclado.Id));

        var bySearch = await _client.GetFromJsonAsync<PagedResponse<OrderResponse>>("/orders?search=zulmira");
        bySearch!.Items.Should().NotBeEmpty();
        bySearch.Items.Should().OnlyContain(o => o.Customer.Contains("Zulmira"));

        var byStatus = await _client.GetFromJsonAsync<PagedResponse<OrderResponse>>("/orders?status=Pendente");
        byStatus!.Items.Should().OnlyContain(o => o.Status == "Pendente");
    }

    [Fact]
    public async Task GetOrders_ClampsAnAbsurdPageSize()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<OrderResponse>>("/orders?pageSize=100000");

        page!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetDashboard_ReturnsASnapshot()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);
        await _client.PostAsJsonAsync("/orders", new CreateOrderRequest("Painel", notebook.Id));

        var response = await _client.GetAsync("/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        snapshot.Should().NotBeNull();
        snapshot!.StatusBreakdown.Should().NotBeEmpty();
        // One bucket per hour across the window, plus the current one.
        snapshot.Throughput.Should().HaveCount(25);
        snapshot.Throughput.Last().IsCurrent.Should().BeTrue();
        snapshot.Throughput.SkipLast(1).Should().OnlyContain(p => !p.IsCurrent);
        snapshot.CreatedToday.Should().BeGreaterThan(0);

        // The per-status parts must add up to the bucket total, or the stacked
        // chart would be drawing a different number than the one it labels.
        snapshot.Throughput.Should().OnlyContain(p =>
            p.Total == p.Pendente + p.Processando + p.Finalizado + p.Falhou + p.Cancelado);
        snapshot.Throughput.Sum(p => p.Total).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// The worker never runs in these tests (the broker host is unreachable on
    /// purpose), so the order is still Pendente - which is exactly the case this
    /// endpoint exists for.
    /// </summary>
    [Fact]
    public async Task PostCancel_CancelsAnOrderStillWaitingForTheWorker()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);
        var create = await _client.PostAsJsonAsync(
            "/orders", new CreateOrderRequest("Ana Prado", notebook.Id));
        var created = await create.Content.ReadFromJsonAsync<OrderResponse>();
        created!.Status.Should().Be("Pendente");

        var response = await _client.PostAsync($"/orders/{created.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<OrderDetailResponse>();
        detail!.Status.Should().Be("Cancelado");
        detail.History.Should().Contain(h => h.Status == "Cancelado");
    }

    [Fact]
    public async Task PostCancel_IsRefusedOnceTheOrderHasSettled()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);
        var create = await _client.PostAsJsonAsync(
            "/orders", new CreateOrderRequest("Bruno Dias", notebook.Id));
        var created = await create.Content.ReadFromJsonAsync<OrderResponse>();

        var first = await _client.PostAsync($"/orders/{created!.Id}/cancel", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cancelado is terminal, so the second attempt is a state conflict.
        var second = await _client.PostAsync($"/orders/{created.Id}/cancel", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostCancel_ReturnsNotFoundForAnUnknownOrder()
    {
        var response = await _client.PostAsync($"/orders/{Guid.NewGuid()}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
