using System.ComponentModel.DataAnnotations;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Api.Dtos;

/// <summary>
/// The caller picks who is ordering and which catalogue product. The amount is
/// resolved server-side from the product, so it is deliberately not accepted here.
///
/// The two processing fields drive the worker and are optional: omitting them
/// gives the default behaviour from the brief (five seconds, then Finalizado).
/// </summary>
public record CreateOrderRequest(
    [Required, MaxLength(200)] string Customer,
    [Required] Guid ProductId,
    [Range(ProcessingPlan.MinDurationSeconds, ProcessingPlan.MaxDurationSeconds)]
    int? ProcessingSeconds = null,
    ProcessingOutcome? Outcome = null)
{
    public ProcessingPlan ToPlan() => new(
        ProcessingSeconds ?? ProcessingPlan.DefaultDurationSeconds,
        Outcome ?? ProcessingOutcome.Concluir);
}
