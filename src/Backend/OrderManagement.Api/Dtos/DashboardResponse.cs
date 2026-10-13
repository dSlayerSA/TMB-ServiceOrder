using OrderManagement.Application.Analytics;

namespace OrderManagement.Api.Dtos;

public record DashboardResponse(
    int InFlight,
    int CreatedToday,
    decimal AmountInFlight,
    double? AverageProcessingSeconds,
    double? CompletionRate,
    IReadOnlyList<StatusSlice> StatusBreakdown,
    IReadOnlyList<ThroughputPoint> Throughput,
    IReadOnlyList<OrderResponse> InFlightOrders);
