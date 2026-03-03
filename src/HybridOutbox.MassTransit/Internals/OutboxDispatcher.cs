using HybridOutbox.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HybridOutbox.MassTransit;

internal sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IBus _bus;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IBus bus,
        ILogger<OutboxDispatcher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (message.DispatcherContext.TryGetValue(Constants.ContextKeys.IsPublish, out var isPublish) && isPublish == "true")
            {
                await DispatchPublishAsync(message, cancellationToken);
                return;
            }

            await DispatchSendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} to {Destination}",
                message.MessageId, message.DestinationAddress);
            throw;
        }
    }

    private async Task DispatchPublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.ClrType is null)
        {
            _logger.LogError("Publish outbox message {MessageId} is missing ClrType — cannot dispatch",
                message.MessageId);
            return;
        }

        var type = Type.GetType(message.ClrType);
        if (type is null)
        {
            _logger.LogError("Cannot resolve CLR type '{ClrType}' for outbox message {MessageId}",
                message.ClrType, message.MessageId);
            return;
        }

        var messageObject = JsonConvert.DeserializeObject(message.Body, type);
        if (messageObject is null)
        {
            _logger.LogWarning("Failed to deserialize publish body for MessageId: {MessageId}", message.MessageId);
            return;
        }

        await _bus.Publish(messageObject, type, cancellationToken);

        _logger.LogDebug("Published outbox message {MessageId} as {ClrType}", message.MessageId, message.ClrType);
    }

    private async Task DispatchSendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(message.DestinationAddress, UriKind.Absolute, out var destinationAddress))
            throw new InvalidOperationException(
                $"Invalid destination address '{message.DestinationAddress}' for message {message.MessageId}");

        var endpoint = await _bus.GetSendEndpoint(destinationAddress);

        var messageObject = JsonConvert.DeserializeObject<JObject>(message.Body);
        if (messageObject == null)
        {
            _logger.LogWarning("Failed to deserialize message body for MessageId: {MessageId}", message.MessageId);
            return;
        }

        await endpoint.Send(messageObject, context =>
        {
            if (Guid.TryParse(message.MessageId, out var messageId))
                context.MessageId = messageId;

            if (!string.IsNullOrEmpty(message.SourceAddress) &&
                Uri.TryCreate(message.SourceAddress, UriKind.Absolute, out var sourceAddress))
                context.SourceAddress = sourceAddress;

            if (!string.IsNullOrEmpty(message.ResponseAddress) &&
                Uri.TryCreate(message.ResponseAddress, UriKind.Absolute, out var responseAddress))
                context.ResponseAddress = responseAddress;

            if (!string.IsNullOrEmpty(message.FaultAddress) &&
                Uri.TryCreate(message.FaultAddress, UriKind.Absolute, out var faultAddress))
                context.FaultAddress = faultAddress;

            if (!string.IsNullOrEmpty(message.CorrelationId) &&
                Guid.TryParse(message.CorrelationId, out var correlationId))
                context.CorrelationId = correlationId;

            if (!string.IsNullOrEmpty(message.ConversationId) &&
                Guid.TryParse(message.ConversationId, out var conversationId))
                context.ConversationId = conversationId;

            if (!string.IsNullOrEmpty(message.InitiatorId) &&
                Guid.TryParse(message.InitiatorId, out var initiatorId))
                context.InitiatorId = initiatorId;

            if (!string.IsNullOrEmpty(message.RequestId) &&
                Guid.TryParse(message.RequestId, out var requestId))
                context.RequestId = requestId;

            foreach (var header in message.Headers)
                context.Headers.Set(header.Key, header.Value);
        }, cancellationToken);

        _logger.LogDebug("Dispatched outbox message {MessageId} to {Destination}",
            message.MessageId, message.DestinationAddress);
    }
}