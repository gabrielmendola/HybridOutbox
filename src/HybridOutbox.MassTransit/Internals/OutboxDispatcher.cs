using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using MassTransit.Serialization;
using Microsoft.Extensions.Logging;

namespace HybridOutbox.MassTransit.Internals;

internal class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IBusControl _busControl;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IBusControl busControl,
        ILogger<OutboxDispatcher> logger)
    {
        _busControl = busControl;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var outboxMessage = OutboxMassTransitMessage.From(message);

        if (outboxMessage.DestinationAddress is null)
            throw new InvalidOperationException(
                $"Invalid destination address '{outboxMessage.RawDestinationAddress}' for message {outboxMessage.MessageId}");

        try
        {
            var endpoint = await _busControl.GetSendEndpoint(outboxMessage.DestinationAddress).ConfigureAwait(false);
            var pipe = new OutboxSendPipe(outboxMessage);

            await endpoint.Send(new SerializedMessageBody(), pipe, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} to {Destination}",
                outboxMessage.MessageId, outboxMessage.RawDestinationAddress);
            throw;
        }
    }
}