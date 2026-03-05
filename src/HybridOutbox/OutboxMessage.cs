namespace HybridOutbox;

public sealed class OutboxMessage
{
    public Guid MessageId { get; init; }
    public Guid? CorrelationId { get; init; }

    public string? DispatcherKind { get; init; }
    public Dictionary<string, string> DispatcherContext { get; init; } = [];

    public string? DestinationAddress { get; init; }
    public string? SourceAddress { get; init; }
    public string? ResponseAddress { get; init; }
    public string? FaultAddress { get; init; }

    public string MessageType { get; init; } = string.Empty;

    public string? ClrType { get; init; }
    public string Body { get; init; } = null!;
    public string ContentType { get; init; } = "application/json";

    public Dictionary<string, object> Headers { get; set; } = [];

    public bool IsProcessed { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LockedAt { get; init; }
}