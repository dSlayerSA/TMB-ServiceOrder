using OrderManagement.Domain.Orders;

namespace OrderManagement.Application.Orders;

public enum OrderSortField
{
    CreatedAt = 0,
    Amount = 1,
    Customer = 2,
    Status = 3
}

/// <summary>
/// Everything the list view can ask for. Paging is not optional: the table grows
/// without bound otherwise, and the client should never be the thing deciding
/// how many rows come back.
/// </summary>
public record OrderQuery(
    int Page = 1,
    int PageSize = 20,
    OrderStatus? Status = null,
    string? Search = null,
    OrderSortField Sort = OrderSortField.CreatedAt,
    bool Ascending = false)
{
    public const int MaxPageSize = 100;

    /// <summary>Clamped rather than rejected: a bad page number is not worth a 400.</summary>
    public OrderQuery Normalized() => this with
    {
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()
    };
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);
}
