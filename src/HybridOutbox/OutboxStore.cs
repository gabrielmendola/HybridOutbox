using System.Threading.Channels;
using HybridOutbox.Abstractions;
using Microsoft.Extensions.Logging;

namespace HybridOutbox;

public abstract class OutboxStore : IOutboxStore
{
    private readonly Dictionary<string, OutboxMessage> _messages = new();
    private readonly ChannelWriter<OutboxMessage> _channel;
    private readonly ILogger<OutboxStore> _logger;

    public OutboxStore(
        ChannelWriter<OutboxMessage> channel,
        ILogger<OutboxStore> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public virtual OutboxMessage[] GetUndispatchedMessages()
    {
        return _messages.Values.ToArray();
    }

    public void DispatchMessages()
    {
        foreach (var message in _messages.Values)
            if (!_channel.TryWrite(message))
                _logger.LogWarning(
                    "In-memory channel is full or closed for message {MessageId}. " +
                    "The recovery job will dispatch it after the processing threshold.",
                    message.MessageId);
    }

    public void Add(OutboxMessage message)
    {
        _messages.TryAdd(message.MessageId, message);
    }

    public void Clear()
    {
        _messages.Clear();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) _messages.Clear();
    }
}