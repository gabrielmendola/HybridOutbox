using System.Threading.Channels;
using FluentAssertions;
using HybridOutbox.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxStoreTests
{
    private sealed class TestOutboxStore : OutboxStore
    {
        public TestOutboxStore(ChannelWriter<OutboxMessage> channel, ILogger<OutboxStore> logger)
            : base(channel, logger) { }
    }

    private readonly Channel<OutboxMessage> _channel;
    private readonly FakeLogger<OutboxStore> _logger;
    private readonly TestOutboxStore _store;

    public OutboxStoreTests()
    {
        _channel = Channel.CreateUnbounded<OutboxMessage>();
        _logger = new FakeLogger<OutboxStore>();
        _store = new TestOutboxStore(_channel.Writer, _logger);
    }

    [Fact]
    public void Add_AddsMessageReturnedByGetUndispatched()
    {
        var message = new OutboxMessage { MessageId = "msg-1", DestinationAddress = "queue://test" };

        _store.Add(message);

        _store.GetUndispatchedMessages().Should().ContainSingle(m => m.MessageId == "msg-1");
    }

    [Fact]
    public void Add_DuplicateMessageId_IsIgnored()
    {
        var message = new OutboxMessage { MessageId = "msg-1" };

        _store.Add(message);
        _store.Add(message);

        _store.GetUndispatchedMessages().Should().HaveCount(1);
    }

    [Fact]
    public void GetUndispatchedMessages_ReturnsAllAddedMessages()
    {
        _store.Add(new OutboxMessage { MessageId = "1" });
        _store.Add(new OutboxMessage { MessageId = "2" });
        _store.Add(new OutboxMessage { MessageId = "3" });

        _store.GetUndispatchedMessages().Should().HaveCount(3);
    }

    [Fact]
    public void GetUndispatchedMessages_ReturnsEmptyWhenNoMessagesAdded()
    {
        _store.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void DispatchMessages_WritesAllMessagesToChannel()
    {
        _store.Add(new OutboxMessage { MessageId = "1" });
        _store.Add(new OutboxMessage { MessageId = "2" });

        _store.DispatchMessages();

        var ids = new List<string>();
        while (_channel.Reader.TryRead(out var msg))
            ids.Add(msg.MessageId);

        ids.Should().BeEquivalentTo(["1", "2"]);
    }

    [Fact]
    public void DispatchMessages_WhenChannelFull_LogsWarning()
    {
        var bounded = Channel.CreateBounded<OutboxMessage>(1);
        var store = new TestOutboxStore(bounded.Writer, _logger);

        bounded.Writer.TryWrite(new OutboxMessage { MessageId = "filler" });

        store.Add(new OutboxMessage { MessageId = "overflow" });
        store.DispatchMessages();

        _logger.HasWarning("overflow").Should().BeTrue();
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        _store.Add(new OutboxMessage { MessageId = "1" });
        _store.Add(new OutboxMessage { MessageId = "2" });

        _store.Clear();

        _store.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_ClearsMessages()
    {
        _store.Add(new OutboxMessage { MessageId = "1" });

        _store.Dispose();

        _store.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void DispatchMessages_AfterClear_WritesNothingToChannel()
    {
        _store.Add(new OutboxMessage { MessageId = "1" });
        _store.Clear();

        _store.DispatchMessages();

        _channel.Reader.TryRead(out _).Should().BeFalse();
    }
}
