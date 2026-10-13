using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Analytics;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardQueries _queries;

    public DashboardController(IDashboardQueries queries)
    {
        _queries = queries;
    }

    /// <summary>
    /// Every figure the dashboard draws, in one round trip.
    /// </summary>
    /// <param name="hours">
    /// Window for the throughput series: 1, 6 or 24. Anything else falls back to
    /// 24. Shorter windows use finer buckets, so a burst in the last few minutes
    /// is visible instead of collapsed into one hourly column.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int hours, CancellationToken ct)
    {
        var snapshot = await _queries.GetAsync(ThroughputWindow.FromHours(hours), ct);

        return Ok(new DashboardResponse(
            snapshot.InFlight,
            snapshot.CreatedToday,
            snapshot.AmountInFlight,
            snapshot.AverageProcessingSeconds,
            snapshot.CompletionRate,
            snapshot.StatusBreakdown,
            snapshot.Throughput,
            snapshot.InFlightOrders.Select(OrderResponse.FromDomain).ToList()));
    }
}
