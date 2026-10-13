using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Catalog;
using OrderManagement.Domain.Messaging;
using OrderManagement.Domain.Orders;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Infrastructure.Persistence;

public class OrderManagementDbContext : DbContext, IUnitOfWork
{
    public OrderManagementDbContext(DbContextOptions<OrderManagementDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderManagementDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        await base.SaveChangesAsync(ct);
    }
}
