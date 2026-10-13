using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly OrderManagementDbContext _db;

    public OrderRepository(OrderManagementDbContext db)
    {
        _db = db;
    }

    public void Add(Order order) => _db.Orders.Add(order);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.StatusHistories).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Order>> GetAllAsync(CancellationToken ct = default) =>
        _db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task<PagedResult<Order>> SearchAsync(OrderQuery query, CancellationToken ct = default)
    {
        var filtered = _db.Orders.AsNoTracking().AsQueryable();

        if (query.Status is not null)
            filtered = filtered.Where(o => o.Status == query.Status);

        if (query.Search is not null)
        {
            // Case-insensitive on both text columns; ILIKE is translated by Npgsql.
            var term = $"%{query.Search}%";
            filtered = filtered.Where(o =>
                EF.Functions.ILike(o.Customer, term) || EF.Functions.ILike(o.Product, term));
        }

        // Counted before paging, so the client knows how many pages exist.
        var total = await filtered.CountAsync(ct);

        var sorted = (query.Sort, query.Ascending) switch
        {
            (OrderSortField.Amount, true) => filtered.OrderBy(o => o.Amount),
            (OrderSortField.Amount, false) => filtered.OrderByDescending(o => o.Amount),
            (OrderSortField.Customer, true) => filtered.OrderBy(o => o.Customer),
            (OrderSortField.Customer, false) => filtered.OrderByDescending(o => o.Customer),
            (OrderSortField.Status, true) => filtered.OrderBy(o => o.Status),
            (OrderSortField.Status, false) => filtered.OrderByDescending(o => o.Status),
            (_, true) => filtered.OrderBy(o => o.CreatedAt),
            (_, false) => filtered.OrderByDescending(o => o.CreatedAt)
        };

        var items = await sorted
            // Id breaks ties so paging stays stable when timestamps collide.
            .ThenBy(o => o.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Order>(items, query.Page, query.PageSize, total);
    }
}
