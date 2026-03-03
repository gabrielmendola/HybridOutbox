using FluentAssertions;
using HybridOutbox.DynamoDb;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbOutboxMessageTests
{
    private static OutboxMessage BuildFullMessage() => new()
    {
        MessageId = "msg-1",
        DestinationAddress = "queue://test",
        SourceAddress = "source://app",
        ResponseAddress = "response://queue",
        FaultAddress = "fault://queue",
        MessageType = ["ns:TypeA", "ns:TypeB"],
        ClrType = "Some.Type, Assembly",
        Body = "{\"key\":\"value\"}",
        ContentType = "application/json",
        CorrelationId = "corr-1",
        ConversationId = "conv-1",
        InitiatorId = "init-1",
        RequestId = "req-1",
        Headers = new Dictionary<string, string> { ["X-Custom"] = "header-value" },
        SentAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc),
        IsProcessed = false,
        ProcessedAt = null,
        Version = 3,
        LockedAt = null,
        RetryCount = 2,
        DispatcherKind = "MassTransit",
        DispatcherContext = new Dictionary<string, string> { ["IsPublish"] = "true" }
    };

    [Fact]
    public void FromMessage_MapsAllScalarFields()
    {
        var message = BuildFullMessage();

        var result = DynamoDbOutboxMessage.FromMessage(message);

        result.MessageId.Should().Be("msg-1");
        result.DestinationAddress.Should().Be("queue://test");
        result.SourceAddress.Should().Be("source://app");
        result.ResponseAddress.Should().Be("response://queue");
        result.FaultAddress.Should().Be("fault://queue");
        result.MessageType.Should().Equal(["ns:TypeA", "ns:TypeB"]);
        result.ClrType.Should().Be("Some.Type, Assembly");
        result.Body.Should().Be("{\"key\":\"value\"}");
        result.ContentType.Should().Be("application/json");
        result.CorrelationId.Should().Be("corr-1");
        result.ConversationId.Should().Be("conv-1");
        result.InitiatorId.Should().Be("init-1");
        result.RequestId.Should().Be("req-1");
        result.Headers.Should().ContainKey("X-Custom").WhoseValue.Should().Be("header-value");
        result.SentAt.Should().Be(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        result.CreatedAt.Should().Be(new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        result.RetryCount.Should().Be(2);
        result.Version.Should().Be(3);
        result.DispatcherKind.Should().Be("MassTransit");
        result.DispatcherContext.Should().ContainKey("IsPublish").WhoseValue.Should().Be("true");
    }

    [Fact]
    public void FromMessage_IsProcessedTrue_MapsToOne()
    {
        var result = DynamoDbOutboxMessage.FromMessage(new OutboxMessage { IsProcessed = true });

        result.IsProcessed.Should().Be(1);
    }

    [Fact]
    public void FromMessage_IsProcessedFalse_MapsToZero()
    {
        var result = DynamoDbOutboxMessage.FromMessage(new OutboxMessage { IsProcessed = false });

        result.IsProcessed.Should().Be(0);
    }

    [Fact]
    public void FromMessage_ExpiresAtIsNull_ByDefault()
    {
        var result = DynamoDbOutboxMessage.FromMessage(new OutboxMessage());

        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ToMessage_MapsAllScalarFields()
    {
        var dynamo = new DynamoDbOutboxMessage
        {
            MessageId = "msg-1",
            DestinationAddress = "queue://test",
            SourceAddress = "source://app",
            ResponseAddress = "response://queue",
            FaultAddress = "fault://queue",
            MessageType = ["ns:TypeA"],
            ClrType = "Some.Type, Assembly",
            Body = "{\"key\":\"value\"}",
            ContentType = "application/json",
            CorrelationId = "corr-1",
            ConversationId = "conv-1",
            InitiatorId = "init-1",
            RequestId = "req-1",
            Headers = new Dictionary<string, string> { ["X-Custom"] = "header-value" },
            SentAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            IsProcessed = 0,
            RetryCount = 2,
            Version = 3,
            DispatcherKind = "MassTransit",
            DispatcherContext = new Dictionary<string, string> { ["IsPublish"] = "false" }
        };

        var result = dynamo.ToMessage();

        result.MessageId.Should().Be("msg-1");
        result.DestinationAddress.Should().Be("queue://test");
        result.SourceAddress.Should().Be("source://app");
        result.Body.Should().Be("{\"key\":\"value\"}");
        result.ContentType.Should().Be("application/json");
        result.RetryCount.Should().Be(2);
        result.Version.Should().Be(3);
        result.DispatcherKind.Should().Be("MassTransit");
        result.DispatcherContext.Should().ContainKey("IsPublish").WhoseValue.Should().Be("false");
    }

    [Fact]
    public void ToMessage_IsProcessedOne_MapsToTrue()
    {
        var result = new DynamoDbOutboxMessage { IsProcessed = 1 }.ToMessage();

        result.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public void ToMessage_IsProcessedZero_MapsToFalse()
    {
        var result = new DynamoDbOutboxMessage { IsProcessed = 0 }.ToMessage();

        result.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var original = BuildFullMessage();
        original.IsProcessed = true;

        var roundTripped = DynamoDbOutboxMessage.FromMessage(original).ToMessage();

        roundTripped.MessageId.Should().Be(original.MessageId);
        roundTripped.DestinationAddress.Should().Be(original.DestinationAddress);
        roundTripped.Body.Should().Be(original.Body);
        roundTripped.IsProcessed.Should().Be(original.IsProcessed);
        roundTripped.RetryCount.Should().Be(original.RetryCount);
        roundTripped.Version.Should().Be(original.Version);
        roundTripped.DispatcherKind.Should().Be(original.DispatcherKind);
        roundTripped.DispatcherContext.Should().BeEquivalentTo(original.DispatcherContext);
        roundTripped.Headers.Should().BeEquivalentTo(original.Headers);
        roundTripped.MessageType.Should().Equal(original.MessageType);
    }
}
