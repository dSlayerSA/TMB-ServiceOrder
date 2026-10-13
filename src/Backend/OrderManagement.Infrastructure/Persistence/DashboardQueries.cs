using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Analytics;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Persistence;

public class DashboardQueries : IDashboardQueries
{
    private const int MaxInFlightShown = 12;

    private static readonly OrderStatus[] InFlightStatuses = [OrderStatus.Pendente, OrderStatus.Processando];

    private static readonly OrderStatus[] SettledStatuses =
        [OrderStatus.Finalizado, OrderStatus.Falhou, OrderStatus.Cancelado];

    private readonly OrderManagementDbContext _db;

    public DashboardQueries(OrderManagementDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSnapshot> GetAsync(ThroughputWindow window, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var windowStart = now.AddHours(-window.Hours);

        var orders = _db.Orders.AsNoTracking();

        var breakdown = await orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.Amount) })
            .ToListAsync(ct);

        var createdToday = await orders.CountAsync(o => o.CreatedAt >= startOfDay, ct);

        // Only the creation timestamps of a bounded 24h window are pulled, then
        // bucketed in memory: Npgsql has no clean translation for date_trunc here
        // and the row count is capped by the window.
        var recent = await orders
            .Where(o => o.CreatedAt >= windowStart)
            .Select(o => new { o.CreatedAt, o.Status })
            .ToListAsync(ct);

        var inFlight = await orders
            .Where(o => o.Status == OrderStatus.Pendente || o.Status == OrderStatus.Processando)
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxInFlightShown)
            .ToListAsync(ct);

        var completed = await orders
            .Where(o => o.Status == OrderStatus.Finalizado)
            .Select(o => new
            {
                o.CreatedAt,
                FinishedAt = o.StatusHistories
                    .Where(h => h.Status == OrderStatus.Finalizado)
                    .Max(h => (DateTime?)h.ChangedAt)
            })
            .ToListAsync(ct);

        var durations = completed
            .Where(c => c.FinishedAt.HasValue)
            .Select(c => (c.FinishedAt!.Value - c.CreatedAt).TotalSeconds)
            .ToList();

        var settled = breakdown.Where(b => SettledStatuses.Contains(b.Status)).Sum(b => b.Count);
        var finished = breakdown.FirstOrDefault(b => b.Status == OrderStatus.Finalizado)?.Count ?? 0;

        return new DashboardSnapshot(
            InFlight: breakdown.Where(b => InFlightStatuses.Contains(b.Status)).Sum(b => b.Count),
            CreatedToday: createdToday,
            AmountInFlight: breakdown.Where(b => InFlightStatuses.Contains(b.Status)).Sum(b => b.Total),
            AverageProcessingSeconds: durations.Count == 0 ? null : Math.Round(durations.Average(), 2),
            CompletionRate: settled == 0 ? null : Math.Round(finished / (double)settled, 4),
            StatusBreakdown: breakdown
                .Select(b => new StatusSlice(b.Status.ToString(), b.Count, b.Total))
                .OrderBy(b => b.Status)
                .ToList(),
            Throughput: BuildThroughput(recent.Select(r => (r.CreatedAt, r.Status)).ToList(), now, window),
            InFlightOrders: inFlight);
    }

    /// <summary>
    /// One point per bucket across the whole window, including the empty ones -
    /// a gap in the series would read as "no data" instead of "no orders".
    /// </summary>
    private static List<ThroughputPoint> BuildThroughput(
        IReadOnlyCollection<(DateTime CreatedAt, OrderStatus Status)> recent,
        DateTime now,
        ThroughputWindow window)
    {
        var size = TimeSpan.FromMinutes(window.BucketMinutes);
        var currentBucket = FloorTo(now, size);

        var byBucket = recent
            .GroupBy(o => FloorTo(o.CreatedAt, size))
            .ToDictionary(g => g.Key, g => g.ToList());

        return Enumerable.Range(0, window.BucketCount + 1)
            .Select(offset => currentBucket - size * (window.BucketCount - offset))
            .Select(bucket =>
            {
                var inBucket = byBucket.GetValueOrDefault(bucket) ?? [];
                int Count(OrderStatus status) => inBucket.Count(o => o.Status == status);

                return new ThroughputPoint(
                    bucket,
                    // The last bucket is still being filled, so it is marked and
                    // must not be read as a completed interval.
                    IsCurrent: bucket == currentBucket,
                    inBucket.Count,
                    Count(OrderStatus.Pendente),
                    Count(OrderStatus.Processando),
                    Count(OrderStatus.Finalizado),
                    Count(OrderStatus.Falhou),
                    Count(OrderStatus.Cancelado));
            })
            .ToList();
    }

    /// <summary>Rounds an instant down to the start of its bucket.</summary>
    private static DateTime FloorTo(DateTime value, TimeSpan size) =>
        new(value.Ticks - value.Ticks % size.Ticks, DateTimeKind.Utc);
}
