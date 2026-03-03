using Amazon.DynamoDBv2.DataModel;

namespace HybridOutbox.DynamoDb;

[DynamoDBTable("OutboxMessages")]
public sealed class DynamoDbOutboxMessage
{
    [DynamoDBHashKey]
    public string MessageId { get; set; } = null!;
    
    [DynamoDBProperty]
    public string? DispatcherKind { get; set; }
    [DynamoDBProperty]
    public Dictionary<string, string> DispatcherContext { get; set; } = [];

    [DynamoDBProperty]
    public string DestinationAddress { get; set; } = null!;
    [DynamoDBProperty]
    public string? SourceAddress { get; set; }
    [DynamoDBProperty]
    public string? ResponseAddress { get; set; }
    [DynamoDBProperty]
    public string? FaultAddress { get; set; }
    [DynamoDBProperty]
    public List<string> MessageType { get; set; } = [];
    [DynamoDBProperty]
    public string Body { get; set; } = null!;
    [DynamoDBProperty]
    public string ContentType { get; set; } = null!;
    [DynamoDBProperty]
    public string? CorrelationId { get; set; }
    [DynamoDBProperty]
    public string? ConversationId { get; set; }
    [DynamoDBProperty]
    public string? InitiatorId { get; set; }
    [DynamoDBProperty]
    public string? RequestId { get; set; }
    [DynamoDBProperty]
    public Dictionary<string, string> Headers { get; set; } = [];
    [DynamoDBProperty]
    public DateTime? SentAt { get; set; }
    [DynamoDBProperty]
    public DateTime CreatedAt { get; set; }
    [DynamoDBProperty]
    public int IsProcessed { get; set; }
    [DynamoDBProperty]
    public DateTime? ProcessedAt { get; set; }
    [DynamoDBVersion]
    public int? Version { get; set; }
    [DynamoDBProperty]
    public DateTime? LockedAt { get; set; }
    [DynamoDBProperty]
    public int RetryCount { get; set; }
    [DynamoDBProperty]
    public string? ClrType { get; set; }
    [DynamoDBProperty]
    public long? ExpiresAt { get; set; }

    public static DynamoDbOutboxMessage FromMessage(OutboxMessage m)
    {
        return new DynamoDbOutboxMessage
        {
            MessageId = m.MessageId,
            DestinationAddress = m.DestinationAddress,
            SourceAddress = m.SourceAddress,
            ResponseAddress = m.ResponseAddress,
            FaultAddress = m.FaultAddress,
            MessageType = m.MessageType,
            Body = m.Body,
            ContentType = m.ContentType,
            CorrelationId = m.CorrelationId,
            ConversationId = m.ConversationId,
            InitiatorId = m.InitiatorId,
            RequestId = m.RequestId,
            Headers = m.Headers,
            SentAt = m.SentAt,
            CreatedAt = m.CreatedAt,
            IsProcessed = m.IsProcessed ? 1 : 0,
            ProcessedAt = m.ProcessedAt,
            Version = m.Version,
            LockedAt = m.LockedAt,
            RetryCount = m.RetryCount,
            DispatcherKind = m.DispatcherKind,
            DispatcherContext = m.DispatcherContext,
            ClrType = m.ClrType
        };
    }

    public OutboxMessage ToMessage()
    {
        return new OutboxMessage
        {
            MessageId = MessageId,
            DestinationAddress = DestinationAddress,
            SourceAddress = SourceAddress,
            ResponseAddress = ResponseAddress,
            FaultAddress = FaultAddress,
            MessageType = MessageType,
            Body = Body,
            ContentType = ContentType,
            CorrelationId = CorrelationId,
            ConversationId = ConversationId,
            InitiatorId = InitiatorId,
            RequestId = RequestId,
            Headers = Headers,
            SentAt = SentAt,
            CreatedAt = CreatedAt,
            IsProcessed = IsProcessed == 1,
            ProcessedAt = ProcessedAt,
            Version = Version,
            LockedAt = LockedAt,
            RetryCount = RetryCount,
            DispatcherKind = DispatcherKind,
            DispatcherContext = DispatcherContext,
            ClrType = ClrType
        };
    }
}
