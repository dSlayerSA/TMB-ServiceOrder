using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Analytics;

/// <summary>
/// The only shape of query the analytics assistant can ask for. Deliberately a
/// closed set of parameters rather than free-form SQL: the language model picks
/// the filters, never the query, so a prompt cannot reach data (or operations)
/// the endpoint does not intend to expose.
/// </summary>
public record OrderAnalyticsFilter(
    OrderStatus? Status = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null);

public record StatusBreakdownItem(string Status, int Count, decimal TotalAmount);

public record OrderAmountResult(int Count, decimal TotalAmount);

public record ProcessingTimeResult(int SampleSize, double? AverageSeconds, double? FastestSeconds, double? SlowestSeconds);

public record OrderSummaryItem(
    Guid Id,
    string Customer,
    string Product,
    decimal Amount,
    string Status,
    DateTime CreatedAt);

/// <summary>Read-only aggregates over orders, used to answer natural-language questions.</summary>
public interface IOrderAnalyticsQueries
{
    Task<int> CountAsync(OrderAnalyticsFilter filter, CancellationToken ct = default);

    Task<OrderAmountResult> SumAmountsAsync(OrderAnalyticsFilter filter, CancellationToken ct = default);

    Task<IReadOnlyList<StatusBreakdownItem>> BreakdownByStatusAsync(OrderAnalyticsFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Wall-clock time from an order being created (Pendente) to reaching
    /// Finalizado, measured from the status history.
    /// </summary>
    Task<ProcessingTimeResult> ProcessingTimeAsync(OrderAnalyticsFilter filter, CancellationToken ct = default);

    Task<IReadOnlyList<OrderSummaryItem>> ListOrdersAsync(OrderAnalyticsFilter filter, int limit, CancellationToken ct = default);
}

public record AnalyticsAnswer(string Answer, IReadOnlyList<string> QueriesUsed);

/// <summary>
/// Answers a natural-language question about the orders in the database.
/// Implemented against an LLM; the Api depends only on this port.
/// </summary>
public interface IOrderAnalyticsAssistant
{
    /// <summary>
    /// False when the module has no credentials configured. The optional module
    /// must not take the rest of the system down, so callers check this (or
    /// handle <see cref="AnalyticsNotConfiguredException"/>) instead of assuming.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Human-readable name of whatever is answering, shown in the UI.</summary>
    string Provider { get; }

    Task<AnalyticsAnswer> AskAsync(string question, CancellationToken ct = default);
}

/// <summary>Thrown when the analytics module is not configured (no API key).</summary>
public class AnalyticsNotConfiguredException : Exception
{
    public AnalyticsNotConfiguredException()
        : base("The analytics assistant is not configured. Set the Anthropic API key to enable it.") { }
}
