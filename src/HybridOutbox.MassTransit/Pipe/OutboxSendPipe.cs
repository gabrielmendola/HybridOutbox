using System.Net.Mime;
using HybridOutbox.MassTransit.Internals;
using MassTransit;
using MassTransit.Transports;

namespace HybridOutbox.MassTransit.Pipe;

internal class OutboxSendPipe : IPipe<SendContext>
{
    private readonly OutboxMassTransitMessage _massTransitMessage;

    public OutboxSendPipe(OutboxMassTransitMessage massTransitMessage)
    {
        _massTransitMessage = massTransitMessage;
    }

    public Task Send(SendContext context)
    {
        var contentType = new ContentType(_massTransitMessage.ContentType);
        var deserializer = context.Serialization.GetMessageDeserializer(contentType);
        var body = deserializer.GetMessageBody(_massTransitMessage.Body);
        var headers = new JsonTransportHeaders(new OutboxMessageHeaderProvider(_massTransitMessage));
        var serializerContext = deserializer.Deserialize(body, headers, _massTransitMessage.DestinationAddress);

        context.MessageId = _massTransitMessage.MessageId;
        context.ConversationId = _massTransitMessage.ConversationId;
        context.InitiatorId = _massTransitMessage.InitiatorId;
        context.RequestId = _massTransitMessage.RequestId;
        context.CorrelationId = _massTransitMessage.CorrelationId;
        context.SourceAddress = _massTransitMessage.SourceAddress;
        context.ResponseAddress = _massTransitMessage.ResponseAddress;
        context.FaultAddress = _massTransitMessage.FaultAddress;
        context.SupportedMessageTypes = _massTransitMessage.MessageType.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        // if (_message.ExpirationTime.HasValue)
        //     context.TimeToLive = _message.ExpirationTime.Value.ToUniversalTime() - DateTime.UtcNow;

        foreach (var headerValue in headers)
            if (headerValue.Key.StartsWith("MT-"))
                context.Headers.Set(headerValue.Key, headerValue.Value);

        foreach (var header in serializerContext.Headers.GetAll())
            context.Headers.Set(header.Key, header.Value);

        // if (_message.Properties.Count > 0 && context is TransportSendContext transportSendContext)
        //     transportSendContext.ReadPropertiesFrom(_message.Properties);

        context.Serializer = serializerContext.GetMessageSerializer();

        return Task.CompletedTask;
    }

    public void Probe(ProbeContext context)
    {
    }
}