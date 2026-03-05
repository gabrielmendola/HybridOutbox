using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.DynamoDb.Internals;

namespace HybridOutbox.DynamoDb;

[DynamoDBTable("HybridOutbox")]
public sealed class DynamoDbOutboxMessage
{
    public const string SortKey = "OUTBOX";

    [DynamoDBHashKey("PK")]
    public Guid PK { get; set; }
    [DynamoDBRangeKey("SK")]
    public string SK { get; set; } = SortKey;
    
    [DynamoDBProperty]
    public Guid? CorrelationId { get; set; }

    [DynamoDBProperty]
    public string? PendingMark { get; set; } = SortKey;

    [DynamoDBProperty]
    public string? DispatcherKind { get; set; }
    [DynamoDBProperty]
    public Dictionary<string, string> DispatcherContext { get; set; } = [];

    [DynamoDBProperty]
    public string? DestinationAddress { get; set; }
    [DynamoDBProperty]
    public string? SourceAddress { get; set; }
    [DynamoDBProperty]
    public string? ResponseAddress { get; set; }
    [DynamoDBProperty]
    public string? FaultAddress { get; set; }
    
    [DynamoDBProperty]
    public string MessageType { get; set; } = string.Empty;
    
    [DynamoDBProperty]
    public string? ClrType { get; set; }
    [DynamoDBProperty]
    public string Body { get; set; } = null!;
    [DynamoDBProperty]
    public string ContentType { get; set; } = null!;
    
    [DynamoDBProperty(typeof(DictionaryObjectConverter))]
    public Dictionary<string, object> Headers { get; set; } = [];
    
    [DynamoDBProperty]
    public bool IsProcessed { get; set; }
    [DynamoDBProperty]
    public DateTime? ProcessedAt { get; set; }
    [DynamoDBProperty]
    public DateTime? SentAt { get; set; }
    [DynamoDBProperty]
    public DateTime CreatedAt { get; set; }
    [DynamoDBProperty]
    public DateTime? LockedAt { get; set; }
    [DynamoDBProperty]
    public long? ExpiresAt { get; set; }

    public static DynamoDbOutboxMessage FromMessage(OutboxMessage m)
    {
        return new DynamoDbOutboxMessage
        {
            PK = m.MessageId,
            DestinationAddress = m.DestinationAddress,
            SourceAddress = m.SourceAddress,
            ResponseAddress = m.ResponseAddress,
            FaultAddress = m.FaultAddress,
            MessageType = m.MessageType,
            Body = m.Body,
            ContentType = m.ContentType,
            CorrelationId = m.CorrelationId,
            Headers = m.Headers,
            SentAt = m.SentAt,
            CreatedAt = m.CreatedAt,
            IsProcessed = m.IsProcessed,
            ProcessedAt = m.ProcessedAt,
            LockedAt = m.LockedAt,
            DispatcherKind = m.DispatcherKind,
            DispatcherContext = m.DispatcherContext,
            ClrType = m.ClrType
        };
    }

    public OutboxMessage ToMessage()
    {
        return new OutboxMessage
        {
            MessageId = PK,
            CorrelationId = CorrelationId,
            DispatcherKind = DispatcherKind,
            DispatcherContext = DispatcherContext,
            DestinationAddress = DestinationAddress,
            SourceAddress = SourceAddress,
            ResponseAddress = ResponseAddress,
            FaultAddress = FaultAddress,
            MessageType = MessageType,
            ClrType = ClrType,
            Body = Body,
            ContentType = ContentType,
            Headers = Headers,
            IsProcessed = IsProcessed,
            ProcessedAt = ProcessedAt,
            SentAt = SentAt,
            CreatedAt = CreatedAt,
            LockedAt = LockedAt,
        };
    }
}