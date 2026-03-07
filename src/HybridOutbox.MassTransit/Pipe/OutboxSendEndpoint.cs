using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Internals;
using MassTransit;
using MassTransit.Context;
using MassTransit.Initializers;
using MassTransit.Transports;
using MassTransit.Util;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxSendEndpoint : ISendEndpoint
{
    private readonly ITransportSendEndpoint _endpoint;
    private readonly IServiceProvider _provider;
    private readonly IOutboxContext _outboxContext;

    internal OutboxSendEndpoint(
        ISendEndpoint sendEndpoint,
        IServiceProvider provider,
        IOutboxContext outboxContext)
    {
        _endpoint = sendEndpoint as ITransportSendEndpoint ??
                    throw new ArgumentException("Must be a transport endpoint", nameof(sendEndpoint));
        _provider = provider;
        _outboxContext = outboxContext;
    }

    public ConnectHandle ConnectSendObserver(ISendObserver observer)
    {
        return new EmptyConnectHandle();
    }

    public Task<SendContext<T>> CreateSendContext<T>(T message, IPipe<SendContext<T>> pipe,
        CancellationToken cancellationToken)
        where T : class
    {
        return _endpoint.CreateSendContext(message, new OutboxSendEndpointPipe<T>(pipe, _provider), cancellationToken);
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken)
        where T : class
    {
        if (message is null) throw new ArgumentNullException(nameof(message));

        var context = await _endpoint
            .CreateSendContext(message, new OutboxSendEndpointPipe<T>(_provider), cancellationToken)
            .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    public async Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken)
        where T : class
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (pipe is null) throw new ArgumentNullException(nameof(pipe));

        var context = await _endpoint
            .CreateSendContext(message, new OutboxSendEndpointPipe<T>(pipe, _provider), cancellationToken)
            .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    public Task Send(object message, CancellationToken cancellationToken)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));

        var messageType = message.GetType();

        return SendEndpointConverterCache.Send(this, message, messageType, cancellationToken);
    }

    public Task Send(object message, Type messageType, CancellationToken cancellationToken)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (messageType is null) throw new ArgumentNullException(nameof(messageType));

        return SendEndpointConverterCache.Send(this, message, messageType, cancellationToken);
    }

    public async Task Send<T>(T message, IPipe<SendContext> pipe, CancellationToken cancellationToken)
        where T : class
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (pipe is null) throw new ArgumentNullException(nameof(pipe));

        var context = await _endpoint
            .CreateSendContext(message, new OutboxSendEndpointPipe<T>(pipe, _provider), cancellationToken)
            .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    public Task Send(object message, IPipe<SendContext> pipe, CancellationToken cancellationToken)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (pipe is null) throw new ArgumentNullException(nameof(pipe));

        var messageType = message.GetType();

        return SendEndpointConverterCache.Send(this, message, messageType, pipe, cancellationToken);
    }

    public Task Send(object message, Type messageType, IPipe<SendContext> pipe, CancellationToken cancellationToken)
    {
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (messageType is null) throw new ArgumentNullException(nameof(messageType));
        if (pipe is null) throw new ArgumentNullException(nameof(pipe));

        return SendEndpointConverterCache.Send(this, message, messageType, pipe, cancellationToken);
    }

    public async Task Send<T>(object values, CancellationToken cancellationToken)
        where T : class
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        var (message, sendPipe) =
            await MessageInitializerCache<T>
                .InitializeMessage(values, new OutboxSendEndpointPipe<T>(_provider), cancellationToken)
                .ConfigureAwait(false);

        var context =
            await _endpoint
                .CreateSendContext(message, new OutboxSendEndpointPipe<T>(sendPipe, _provider), cancellationToken)
                .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    public async Task Send<T>(object values, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken)
        where T : class
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        var (message, sendPipe) =
            await MessageInitializerCache<T>.InitializeMessage(values, new OutboxSendEndpointPipe<T>(pipe, _provider),
                    cancellationToken)
                .ConfigureAwait(false);

        var context =
            await _endpoint
                .CreateSendContext(message, new OutboxSendEndpointPipe<T>(sendPipe, _provider), cancellationToken)
                .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    public async Task Send<T>(object values, IPipe<SendContext> pipe, CancellationToken cancellationToken)
        where T : class
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        if (pipe is null) throw new ArgumentNullException(nameof(pipe));

        var (message, sendPipe) =
            await MessageInitializerCache<T>.InitializeMessage(values, new OutboxSendEndpointPipe<T>(pipe, _provider),
                    cancellationToken)
                .ConfigureAwait(false);

        var context =
            await _endpoint
                .CreateSendContext(message, new OutboxSendEndpointPipe<T>(sendPipe, _provider), cancellationToken)
                .ConfigureAwait(false);

        await Send(context).ConfigureAwait(false);
    }

    private async Task Send<T>(SendContext<T> context)
        where T : class
    {
        var message = new OutboxMessage
        {
            MessageId = context.MessageId.Value,
            CorrelationId = context.CorrelationId,
            DispatcherKind = OutboxConstants.DispatcherKind,
            DispatcherContext = new Dictionary<string, string>
            {
                [OutboxConstants.ConversationId] = context.ConversationId.ToString(),
                [OutboxConstants.InitiatorId] = context.InitiatorId?.ToString(),
                [OutboxConstants.RequestId] = context.RequestId?.ToString()
            },
            SentAt = context.SentTime ?? DateTime.UtcNow,
            Headers = context.Headers.ToDictionary(x => x.Key, x => x.Value),
            ContentType = context.ContentType?.ToString() ?? context.Serialization.DefaultContentType.ToString(),
            MessageType = string.Join(";", context.SupportedMessageTypes),
            ClrType = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!,
            Body = context.Serializer.GetMessageBody(context).GetString(),
            SourceAddress = context.SourceAddress?.ToString(),
            DestinationAddress = context.DestinationAddress?.ToString(),
            ResponseAddress = context.ResponseAddress?.ToString(),
            FaultAddress = context.FaultAddress?.ToString()
            // InboxMessageId = inboxMessageId,
            // InboxConsumerId = inboxConsumerId
        };

        _outboxContext.Add(message);
    }
}