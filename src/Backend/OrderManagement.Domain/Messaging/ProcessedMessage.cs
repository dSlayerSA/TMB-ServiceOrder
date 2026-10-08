namespace OrderManagement.Domain.Messaging;

/// <summary>
/// Tracks broker message ids already handled by a consumer, to make
/// message processing idempotent in the face of at-least-once redelivery.
/// </summary>
public class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId)
    {
        MessageId = messageId;
        ProcessedAt = DateTime.UtcNow;
    }
}
