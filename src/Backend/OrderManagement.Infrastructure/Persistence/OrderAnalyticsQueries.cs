using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Analytics;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Persistence;

public class OrderAnalyticsQueries : IOrderAnalyticsQueries
{
    private readonly OrderManagementDbContext _db;

    public OrderAnalyticsQueries(OrderManagementDbContext db)
    {
        _db = db;
    }

    public Task<int> CountAsync(OrderAnalyticsFilter filter, CancellationToken ct = default) =>
        Filtered(filter).CountAsync(ct);

    public async Task<OrderAmountResult> SumAmountsAsync(OrderAnalyticsFilter filter, CancellationToken ct = default)
    {
        var result = await Filtered(filter)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(o => o.Amount) })
            .FirstOrDefaultAsync(ct);

        return new OrderAmountResult(result?.Count ?? 0, result?.Total ?? 0m);
    }

    public async Task<IReadOnlyList<StatusBreakdownItem>> BreakdownByStatusAsync(
        OrderAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        var rows = await Filtered(filter)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.Amount) })
            .ToListAsync(ct);

        return rows
            .Select(r => new StatusBreakdownItem(r.Status.ToString(), r.Count, r.Total))
            .OrderBy(r => r.Status)
            .ToList();
    }

    public async Task<ProcessingTimeResult> ProcessingTimeAsync(
        OrderAnalyticsFilter filter,
        CancellationToken ct = default)
    {
        // The two timestamps are fetched and subtracted in memory rather than in
        // SQL: Npgsql has no clean translation for "interval to seconds" here, and
        // the projection is two columns per finalized order.
        var pairs = await Filtered(filter)
            .Where(o => o.Status == OrderStatus.Finalizado)
            .Select(o => new
            {
                o.CreatedAt,
                FinalizedAt = o.StatusHistories
                    .Where(h => h.Status == OrderStatus.Finalizado)
                    .Max(h => (DateTime?)h.ChangedAt)
            })
            .ToListAsync(ct);

        var durations = pairs
            .Where(p => p.FinalizedAt.HasValue)
            .Select(p => (p.FinalizedAt!.Value - p.CreatedAt).TotalSeconds)
            .ToList();

        if (durations.Count == 0)
            return new ProcessingTimeResult(0, null, null, null);

        return new ProcessingTimeResult(
            durations.Count,
            Math.Round(durations.Average(), 2),
            Math.Round(durations.Min(), 2),
            Math.Round(durations.Max(), 2));
    }

    public async Task<IReadOnlyList<OrderSummaryItem>> ListOrdersAsync(
        OrderAnalyticsFilter filter,
        int limit,
        CancellationToken ct = default)
    {
        var rows = await Filtered(filter)
            .OrderByDescending(o => o.CreatedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(o => new OrderSummaryItem(
                o.Id,
                o.Customer,
                o.Product,
                o.Amount,
                o.Status.ToString(),
                o.CreatedAt))
            .ToListAsync(ct);

        return rows;
    }

    private IQueryable<Order> Filtered(OrderAnalyticsFilter filter)
    {
        var query = _db.Orders.AsNoTracking();

        if (filter.Status is not null)
            query = query.Where(o => o.Status == filter.Status);

        if (filter.CreatedFromUtc is not null)
            query = query.Where(o => o.CreatedAt >= filter.CreatedFromUtc);

        // Exclusive upper bound, so a caller can express "this month" as
        // [first of month, first of next month) without double-counting.
        if (filter.CreatedToUtc is not null)
            query = query.Where(o => o.CreatedAt < filter.CreatedToUtc);

        return query;
    }
}
