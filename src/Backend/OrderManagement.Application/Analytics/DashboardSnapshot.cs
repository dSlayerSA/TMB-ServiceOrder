using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Analytics;

public record StatusSlice(string Status, int Count, decimal TotalAmount);

/// <summary>
/// Orders created in one hour, split by the status they are in now. The parts
/// always sum to <see cref="Total"/>, which is what makes the stack honest.
/// </summary>
public record ThroughputPoint(
    DateTime BucketUtc,
    /// <summary>True for the bucket still being filled - it is not comparable to the closed ones.</summary>
    bool IsCurrent,
    int Total,
    int Pendente,
    int Processando,
    int Finalizado,
    int Falhou,
    int Cancelado);

/// <summary>
/// Everything the dashboard draws, in one round trip. Computed server-side on
/// purpose: the client must never have to pull every order just to count them.
/// </summary>
public record DashboardSnapshot(
    int InFlight,
    int CreatedToday,
    decimal AmountInFlight,
    double? AverageProcessingSeconds,
    /// <summary>Share of settled orders that reached Finalizado. Null when nothing has settled yet.</summary>
    double? CompletionRate,
    IReadOnlyList<StatusSlice> StatusBreakdown,
    IReadOnlyList<ThroughputPoint> Throughput,
    /// <summary>The orders still moving, so the board does not need a second request.</summary>
    IReadOnlyList<Order> InFlightOrders);

/// <summary>
/// How far back the throughput series looks, and therefore how fine its buckets
/// are. A whole day in hourly buckets hides a burst that happened in the last
/// ten minutes, so the window is a parameter rather than a constant.
/// </summary>
public record ThroughputWindow(int Hours, int BucketMinutes)
{
    public static readonly ThroughputWindow LastHour = new(1, 5);
    public static readonly ThroughputWindow SixHours = new(6, 30);
    public static readonly ThroughputWindow Day = new(24, 60);

    public static ThroughputWindow FromHours(int hours) => hours switch
    {
        1 => LastHour,
        6 => SixHours,
        _ => Day
    };

    public int BucketCount => Hours * 60 / BucketMinutes;
}

public interface IDashboardQueries
{
    Task<DashboardSnapshot> GetAsync(ThroughputWindow window, CancellationToken ct = default);
}
