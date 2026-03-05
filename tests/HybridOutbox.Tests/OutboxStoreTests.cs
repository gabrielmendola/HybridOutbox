using System.Threading.Channels;
using FluentAssertions;
using HybridOutbox.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxStoreTests
{
    private static readonly Guid Id1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Id2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Id3 = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid OverflowId = new("00000000-0000-0000-0000-000000000099");

    private sealed class TestOutboxContext : OutboxContext
    {
        public TestOutboxContext(ChannelWriter<OutboxMessage> channel, ILogger<OutboxContext> logger)
            : base(channel, logger)
        {
        }
    }

    private readonly Channel<OutboxMessage> _channel;
    private readonly FakeLogger<OutboxContext> _logger;
    private readonly TestOutboxContext _outboxContext;

    public OutboxStoreTests()
    {
        _channel = Channel.CreateUnbounded<OutboxMessage>();
        _logger = new FakeLogger<OutboxContext>();
        _outboxContext = new TestOutboxContext(_channel.Writer, _logger);
    }

    [Fact]
    public void Add_AddsMessageReturnedByGetUndispatched()
    {
        var message = new OutboxMessage { MessageId = Id1, DestinationAddress = "queue://test" };

        _outboxContext.Add(message);

        _outboxContext.GetUndispatchedMessages().Should().ContainSingle(m => m.MessageId == Id1);
    }

    [Fact]
    public void GetUndispatchedMessages_ReturnsAllAddedMessages()
    {
        _outboxContext.Add(new OutboxMessage { MessageId = Id1 });
        _outboxContext.Add(new OutboxMessage { MessageId = Id2 });
        _outboxContext.Add(new OutboxMessage { MessageId = Id3 });

        _outboxContext.GetUndispatchedMessages().Should().HaveCount(3);
    }

    [Fact]
    public void GetUndispatchedMessages_ReturnsEmptyWhenNoMessagesAdded()
    {
        _outboxContext.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void DispatchMessages_WritesAllMessagesToChannel()
    {
        _outboxContext.Add(new OutboxMessage { MessageId = Id1 });
        _outboxContext.Add(new OutboxMessage { MessageId = Id2 });

        _outboxContext.DispatchMessages();

        var ids = new List<Guid>();
        while (_channel.Reader.TryRead(out var msg))
            ids.Add(msg.MessageId);

        ids.Should().BeEquivalentTo([Id1, Id2]);
    }

    [Fact]
    public void DispatchMessages_WhenChannelFull_LogsWarning()
    {
        var bounded = Channel.CreateBounded<OutboxMessage>(1);
        var outboxContext = new TestOutboxContext(bounded.Writer, _logger);

        bounded.Writer.TryWrite(new OutboxMessage { MessageId = Guid.NewGuid() });

        outboxContext.Add(new OutboxMessage { MessageId = OverflowId });
        outboxContext.DispatchMessages();

        _logger.HasWarning(OverflowId.ToString()).Should().BeTrue();
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        _outboxContext.Add(new OutboxMessage { MessageId = Id1 });
        _outboxContext.Add(new OutboxMessage { MessageId = Id2 });

        _outboxContext.Clear();

        _outboxContext.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_ClearsMessages()
    {
        _outboxContext.Add(new OutboxMessage { MessageId = Id1 });

        _outboxContext.Dispose();

        _outboxContext.GetUndispatchedMessages().Should().BeEmpty();
    }

    [Fact]
    public void DispatchMessages_AfterClear_WritesNothingToChannel()
    {
        _outboxContext.Add(new OutboxMessage { MessageId = Id1 });
        _outboxContext.Clear();

        _outboxContext.DispatchMessages();

        _channel.Reader.TryRead(out _).Should().BeFalse();
    }
}