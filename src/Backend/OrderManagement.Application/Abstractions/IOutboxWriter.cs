using OrderManagement.Domain.Outbox;

namespace OrderManagement.Application.Abstractions;

public interface IOutboxWriter
{
    void Enqueue(OutboxMessage message);
}
