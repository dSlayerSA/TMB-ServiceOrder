using System.Net.Http.Json;
using OrderManagement.Api.Dtos;

namespace OrderManagement.Tests.Integration;

/// <summary>
/// Orders can only be placed against a seeded catalogue product, so the tests
/// read the catalogue through the API instead of hard-coding seeded ids - that
/// keeps them honest if the seed changes.
/// </summary>
internal static class Catalogue
{
    public const string Notebook = "Notebook Pro 14";
    public const string Mouse = "Mouse sem fio";
    public const string Teclado = "Teclado mecânico";

    public static async Task<ProductResponse> GetAsync(HttpClient client, string name)
    {
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/products");
        return products!.Single(p => p.Name == name);
    }
}
