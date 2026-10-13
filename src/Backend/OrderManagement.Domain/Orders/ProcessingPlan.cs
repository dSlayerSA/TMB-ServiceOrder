namespace OrderManagement.Domain.Orders;

/// <summary>
/// How long the worker should spend on an order and how it should end.
///
/// The original brief fixes this at "five seconds, then Finalizado". Making it
/// per-order turns the system into something you can actually drive: several
/// orders with different durations and different endings can be in flight at
/// once, which is what the dashboard shows and what makes failure paths
/// reproducible instead of theoretical.
/// </summary>
public class ProcessingPlan
{
    public const int DefaultDurationSeconds = 5;
    public const int MinDurationSeconds = 1;
    public const int MaxDurationSeconds = 120;

    public int DurationSeconds { get; private set; }
    public ProcessingOutcome Outcome { get; private set; }

    private ProcessingPlan() { }

    public ProcessingPlan(int durationSeconds, ProcessingOutcome outcome)
    {
        if (durationSeconds < MinDurationSeconds || durationSeconds > MaxDurationSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                $"Processing time must be between {MinDurationSeconds} and {MaxDurationSeconds} seconds.");
        }

        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown processing outcome.");

        DurationSeconds = durationSeconds;
        Outcome = outcome;
    }

    public static ProcessingPlan Default => new(DefaultDurationSeconds, ProcessingOutcome.Concluir);

    /// <summary>The status this plan drives the order to once the time is up.</summary>
    public OrderStatus FinalStatus => Outcome switch
    {
        ProcessingOutcome.Falhar => OrderStatus.Falhou,
        ProcessingOutcome.Cancelar => OrderStatus.Cancelado,
        _ => OrderStatus.Finalizado
    };
}
