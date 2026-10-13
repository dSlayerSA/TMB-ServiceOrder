using System.Net.Http.Json;
using OrderManagement.Api.Dtos;
using OrderManagement.Tests.Integration;

namespace OrderManagement.Tests.Golden;

/// <summary>
/// Golden tests over the *raw HTTP bodies* served by the running Api. The
/// snapshots in <see cref="OrderContractGoldenTests"/> assume ASP.NET Core's
/// camelCase defaults; these go through the real pipeline, so they would also
/// catch a change to the serializer configuration in Program.cs.
///
/// This class gets its own <see cref="CustomWebApplicationFactory"/> (and so its
/// own throwaway database): xUnit v2 only drives IAsyncLifetime for class
/// fixtures, not collection fixtures, so a shared container would never start.
/// </summary>
public class OrdersApiGoldenTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersApiGoldenTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostOrders_ResponseBodyContractIsStable()
    {
        var notebook = await Catalogue.GetAsync(_client, Catalogue.Notebook);

        var response = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Maria Silva", notebook.Id));

        response.EnsureSuccessStatusCode();

        JsonGolden.VerifyJson("http-post-orders-201", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetOrderById_ResponseBodyContractIsStable()
    {
        var mouse = await Catalogue.GetAsync(_client, Catalogue.Mouse);

        var created = await _client.PostAsJsonAsync(
            "/orders",
            new CreateOrderRequest("Joao Souza", mouse.Id));
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>();

        var response = await _client.GetAsync($"/orders/{order!.Id}");
        response.EnsureSuccessStatusCode();

        JsonGolden.VerifyJson("http-get-order-detail-200", await response.Content.ReadAsStringAsync());
    }
}
