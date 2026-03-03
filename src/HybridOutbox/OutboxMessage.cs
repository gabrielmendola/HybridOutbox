namespace HybridOutbox;

public sealed class OutboxMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    public string? DispatcherKind { get; set; }
    public Dictionary<string, string> DispatcherContext { get; set; } = [];

    public string DestinationAddress { get; set; } = null!;
    public string? SourceAddress { get; set; }
    public string? ResponseAddress { get; set; }
    public string? FaultAddress { get; set; }

    public List<string> MessageType { get; set; } = [];

    public string? ClrType { get; set; }
    public string Body { get; set; } = null!;
    public string ContentType { get; set; } = "application/json";

    public string? CorrelationId { get; set; }
    public string? ConversationId { get; set; }
    public string? InitiatorId { get; set; }
    public string? RequestId { get; set; }

    public Dictionary<string, string> Headers { get; set; } = [];

    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public int? Version { get; set; }
    public DateTime? LockedAt { get; set; }
    public int RetryCount { get; set; }
}
