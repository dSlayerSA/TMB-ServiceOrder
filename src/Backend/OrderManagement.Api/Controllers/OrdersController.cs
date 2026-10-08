using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Orders;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.CreateOrderAsync(request.Customer, request.Product, request.Amount, ct);
        var response = OrderResponse.FromDomain(order);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var orders = await _orderService.GetAllAsync(ct);
        return Ok(orders.Select(OrderResponse.FromDomain));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var order = await _orderService.GetByIdAsync(id, ct);
            return Ok(OrderDetailResponse.FromDomain(order));
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
    }
}
