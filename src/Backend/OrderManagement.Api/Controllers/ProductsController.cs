using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Catalog;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly IProductCatalog _catalog;

    public ProductsController(IProductCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Catalogue available for new orders, with the current price of each item.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var products = await _catalog.GetAllAsync(ct);
        return Ok(products.Select(ProductResponse.FromDomain));
    }
}
