namespace OrderManagement.Domain.Outbox;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string eventType, Guid correlationId, string payload)
    {
        Id = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        EventType = eventType;
        CorrelationId = correlationId;
        Payload = payload;
        Attempts = 0;
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Error = error;
    }
}
