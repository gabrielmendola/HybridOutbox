namespace HybridOutbox.MassTransit.Internals;

internal sealed class OutboxMassTransitMessage
{
    public Guid MessageId { get; init; }

    public string RawDestinationAddress { get; init; } = null!;
    public Uri? DestinationAddress { get; init; }
    public Uri? SourceAddress { get; init; }
    public Uri? ResponseAddress { get; init; }
    public Uri? FaultAddress { get; init; }

    public Guid? CorrelationId { get; init; }
    public Guid? ConversationId { get; init; }
    public Guid? InitiatorId { get; init; }
    public Guid? RequestId { get; init; }

    public string Body { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public string MessageType { get; init; } = string.Empty;
    public string? ClrType { get; init; }
    public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();

    private OutboxMassTransitMessage()
    {
    }

    public static OutboxMassTransitMessage From(OutboxMessage message)
    {
        return new OutboxMassTransitMessage
        {
            MessageId = message.MessageId,
            RawDestinationAddress = message.DestinationAddress,
            DestinationAddress = Uri.TryCreate(message.DestinationAddress, UriKind.Absolute, out var da) ? da : null,
            SourceAddress = Uri.TryCreate(message.SourceAddress, UriKind.Absolute, out var sa) ? sa : null,
            ResponseAddress = Uri.TryCreate(message.ResponseAddress, UriKind.Absolute, out var ra) ? ra : null,
            FaultAddress = Uri.TryCreate(message.FaultAddress, UriKind.Absolute, out var fa) ? fa : null,
            CorrelationId = message.CorrelationId,
            ConversationId = ParseGuid(message.DispatcherContext, OutboxConstants.ConversationId),
            InitiatorId = ParseGuid(message.DispatcherContext, OutboxConstants.InitiatorId),
            RequestId = ParseGuid(message.DispatcherContext, OutboxConstants.RequestId),
            Body = message.Body,
            ContentType = message.ContentType,
            MessageType = message.MessageType,
            ClrType = message.ClrType,
            Headers = message.Headers
        };
    }

    private static Guid? ParseGuid(Dictionary<string, string> context, string key)
    {
        return context.TryGetValue(key, out var val) && Guid.TryParse(val, out var g) ? g : null;
    }
}