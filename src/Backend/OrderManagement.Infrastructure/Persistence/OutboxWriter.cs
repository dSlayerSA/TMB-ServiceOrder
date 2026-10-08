using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Outbox;

namespace OrderManagement.Infrastructure.Persistence;

public class OutboxWriter : IOutboxWriter
{
    private readonly OrderManagementDbContext _db;

    public OutboxWriter(OrderManagementDbContext db)
    {
        _db = db;
    }

    public void Enqueue(OutboxMessage message) => _db.OutboxMessages.Add(message);
}
