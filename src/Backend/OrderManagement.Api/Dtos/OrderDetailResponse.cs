using OrderManagement.Domain.Orders;

namespace OrderManagement.Api.Dtos;

public record OrderStatusHistoryItem(string Status, DateTime ChangedAt, string? Note)
{
    public static OrderStatusHistoryItem FromDomain(OrderStatusHistory history) => new(
        history.Status.ToString(),
        history.ChangedAt,
        history.Note);
}

public record OrderDetailResponse(
    Guid Id,
    string Customer,
    string Product,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ProcessingSeconds,
    string Outcome,
    IReadOnlyList<OrderStatusHistoryItem> History)
{
    public static OrderDetailResponse FromDomain(Order order) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.Status.ToString(),
        order.CreatedAt,
        order.UpdatedAt,
        order.Plan.DurationSeconds,
        order.Plan.Outcome.ToString(),
        order.StatusHistories
            .OrderBy(h => h.ChangedAt)
            .Select(OrderStatusHistoryItem.FromDomain)
            .ToList());
}
