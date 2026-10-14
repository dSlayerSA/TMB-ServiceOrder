using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Catalog;
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await _orderService.CreateOrderAsync(
                request.Customer, request.ProductId, request.ToPlan(), ct);
            var response = OrderResponse.FromDomain(order);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? nameof(request.ProcessingSeconds), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (ProductNotFoundException ex)
        {
            // The product id is part of the payload, so an unknown one is a bad
            // request rather than a missing resource.
            ModelState.AddModelError(nameof(request.ProductId), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Paged, filterable list for the orders table. Paging is always applied -
    /// there is no "give me everything" mode.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] OrderQuery query, CancellationToken ct)
    {
        var page = await _orderService.SearchAsync(query, ct);

        return Ok(new PagedResponse<OrderResponse>(
            page.Items.Select(OrderResponse.FromDomain).ToList(),
            page.Page,
            page.PageSize,
            page.Total,
            page.TotalPages));
    }

    /// <summary>
    /// Cancels an order that has not settled yet. Works while it is still
    /// Pendente - that is, before the worker has even picked it up.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            var order = await _orderService.CancelAsync(id, ct);
            return Ok(OrderDetailResponse.FromDomain(order));
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
        catch (OrderNotCancellableException ex)
        {
            // The order exists, the request is well formed - it is the current
            // state that refuses, which is what 409 is for.
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Pedido já encerrado",
                Detail = ex.Message,
            });
        }
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
