using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
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
}
