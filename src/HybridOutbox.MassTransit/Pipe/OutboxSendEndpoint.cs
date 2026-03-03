using HybridOutbox.Abstractions;
using MassTransit;
using Newtonsoft.Json;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxSendEndpoint : ISendEndpoint
{
    private readonly ISendEndpoint _sendEndpoint;
    private readonly IOutboxStore _store;
    private readonly OutboxDispatchContext _dispatchContext;
    private readonly bool _isPublish;
    private readonly Uri? _address;

    internal OutboxSendEndpoint(
        ISendEndpoint sendEndpoint,
        IOutboxStore store,
        OutboxDispatchContext dispatchContext,
        Uri? address = null,
        bool isPublish = false)
    {
        _sendEndpoint = sendEndpoint;
        _store = store;
        _dispatchContext = dispatchContext;
        _address = address;
        _isPublish = isPublish;
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, cancellationToken);
            return;
        }

        _store.Add(BuildOutboxMessage(message));
    }

    public async Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, pipe, cancellationToken);
            return;
        }

        _store.Add(BuildOutboxMessage(message));
    }

    public async Task Send<T>(T message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, pipe, cancellationToken);
            return;
        }

        _store.Add(BuildOutboxMessage(message));
    }

    public async Task Send<T>(object message, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send<T>(message, cancellationToken);
            return;
        }

        await SendObject(message, typeof(T));
    }

    public async Task Send<T>(object message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, pipe, cancellationToken);
            return;
        }

        await SendObject(message, typeof(T));
    }

    public async Task Send<T>(object message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send<T>(message, pipe, cancellationToken);
            return;
        }

        await SendObject(message, typeof(T));
    }

    public async Task Send(object message, CancellationToken cancellationToken = default)
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, cancellationToken);
            return;
        }

        await SendObject(message, message.GetType());
    }

    public async Task Send(object message, Type messageType, CancellationToken cancellationToken = default)
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, messageType, cancellationToken);
            return;
        }

        await SendObject(message, messageType);
    }

    public async Task Send(object message, IPipe<SendContext> pipe, CancellationToken cancellationToken = default)
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, pipe, cancellationToken);
            return;
        }

        await SendObject(message, message.GetType());
    }

    public async Task Send(object message, Type messageType, IPipe<SendContext> pipe,
        CancellationToken cancellationToken = default)
    {
        if (_dispatchContext.IsOutboxDispatching)
        {
            await _sendEndpoint.Send(message, messageType, pipe, cancellationToken);
            return;
        }

        await SendObject(message, messageType);
    }

    public ConnectHandle ConnectSendObserver(ISendObserver observer)
    {
        return _sendEndpoint.ConnectSendObserver(observer);
    }

    private Task SendObject(object message, Type messageType)
    {
        var buildMethod = typeof(OutboxSendEndpoint)
            .GetMethod(nameof(BuildOutboxMessage),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(messageType);

        var outboxMessage = (OutboxMessage)buildMethod.Invoke(this, [message])!;
        _store.Add(outboxMessage);
        return Task.CompletedTask;
    }

    private OutboxMessage BuildOutboxMessage<T>(T message) where T : class
    {
        return new OutboxMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            DispatcherKind = Constants.DispatcherKind,
            DispatcherContext = new Dictionary<string, string>
            {
                [Constants.ContextKeys.IsPublish] = _isPublish ? "true" : "false"
            },
            ClrType = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!,
            MessageType = MessageTypeCache<T>.MessageTypeNames.ToList(),
            Body = JsonConvert.SerializeObject(message),
            DestinationAddress = _address?.ToString() ?? string.Empty,
            SentAt = DateTime.UtcNow
        };
    }
}