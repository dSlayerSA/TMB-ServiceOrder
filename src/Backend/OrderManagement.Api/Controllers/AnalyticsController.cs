using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Dtos;
using OrderManagement.Application.Analytics;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IOrderAnalyticsAssistant _assistant;

    public AnalyticsController(IOrderAnalyticsAssistant assistant)
    {
        _assistant = assistant;
    }

    /// <summary>
    /// Answers a natural-language question about the orders, e.g.
    /// "Quantos pedidos estão pendentes?".
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(AnalyticsAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask([FromBody] AskAnalyticsRequest request, CancellationToken ct)
    {
        try
        {
            var answer = await _assistant.AskAsync(request.Question, ct);
            return Ok(new AnalyticsAnswerResponse(answer.Answer, answer.QueriesUsed));
        }
        catch (AnalyticsNotConfiguredException ex)
        {
            // The module is optional: without a key the rest of the system still
            // runs, and the frontend uses this to hide the feature.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    /// <summary>Lets the frontend know whether the module is usable before offering it.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus() =>
        Ok(new { enabled = _assistant.IsEnabled, provider = _assistant.Provider });
}
