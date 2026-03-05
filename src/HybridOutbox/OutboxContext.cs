using System.Threading.Channels;
using HybridOutbox.Abstractions;
using Microsoft.Extensions.Logging;

namespace HybridOutbox;

public abstract class OutboxContext : IOutboxContext
{
    private readonly List<OutboxMessage> _messages = [];
    private readonly ChannelWriter<OutboxMessage> _channel;
    private readonly ILogger<OutboxContext> _logger;
    private InboxMessage? _stagedInbox;

    protected OutboxContext(ChannelWriter<OutboxMessage> channel, ILogger<OutboxContext> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected InboxMessage? GetStagedInboxMessage()
    {
        return _stagedInbox;
    }

    public virtual OutboxMessage[] GetUndispatchedMessages()
    {
        return _messages.ToArray();
    }

    public void DispatchMessages()
    {
        foreach (var message in _messages)
            if (!_channel.TryWrite(message))
                _logger.LogWarning(
                    "In-memory channel is full or closed for message {MessageId}. " +
                    "The recovery job will dispatch it after the processing threshold.",
                    message.MessageId);
    }

    public void Add(OutboxMessage message)
    {
        _messages.Add(message);
    }

    public void Add(InboxMessage message)
    {
        _stagedInbox = message;
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
        if (disposing)
        {
            _messages.Clear();
            _stagedInbox = null;
        }
    }
}