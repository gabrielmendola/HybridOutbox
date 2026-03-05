namespace HybridOutbox;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string ConsumerType { get; set; } = null!;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}