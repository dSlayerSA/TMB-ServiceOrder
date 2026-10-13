using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Catalog;
using OrderManagement.Domain.Catalog;

namespace OrderManagement.Infrastructure.Persistence;

public class ProductCatalog : IProductCatalog
{
    private readonly OrderManagementDbContext _db;

    public ProductCatalog(OrderManagementDbContext db)
    {
        _db = db;
    }

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default) =>
        _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
}
