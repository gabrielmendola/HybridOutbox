using FluentAssertions;
using HybridOutbox.DynamoDb;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbOutboxMessageTests
{
    private static readonly Guid MsgId = new("00000001-0000-0000-0000-000000000000");
    private static readonly Guid ConvId = new("00000000-0000-0000-0000-000000000011");
    private static readonly Guid InitId = new("00000000-0000-0000-0000-000000000012");
    private static readonly Guid ReqId = new("00000000-0000-0000-0000-000000000013");
    private static readonly Guid CorrId = new("00000000-0000-0000-0000-000000000099");

    private static OutboxMessage BuildFullMessage()
    {
        return new OutboxMessage
        {
            MessageId = MsgId,
            DestinationAddress = "queue://test",
            SourceAddress = "source://app",
            ResponseAddress = "response://queue",
            FaultAddress = "fault://queue",
            MessageType = "ns:TypeA;ns:TypeB",
            ClrType = "Some.Type, Assembly",
            Body = "{\"key\":\"value\"}",
            ContentType = "application/json",
            CorrelationId = CorrId,
            Headers = new Dictionary<string, object> { ["X-Custom"] = "header-value" },
            SentAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            IsProcessed = false,
            ProcessedAt = null,
            LockedAt = null,
            DispatcherKind = "MassTransit",
            DispatcherContext = new Dictionary<string, string>
            {
                ["IsPublish"] = "true",
                ["ConversationId"] = ConvId.ToString(),
                ["InitiatorId"] = InitId.ToString(),
                ["RequestId"] = ReqId.ToString()
            }
        };
    }

    [Fact]
    public void FromMessage_MapsAllScalarFields()
    {
        var message = BuildFullMessage();

        var result = DynamoDbOutboxMessage.FromMessage(message);

        result.PK.Should().Be(MsgId);
        result.DestinationAddress.Should().Be("queue://test");
        result.SourceAddress.Should().Be("source://app");
        result.ResponseAddress.Should().Be("response://queue");
        result.FaultAddress.Should().Be("fault://queue");
        result.MessageType.Should().Be("ns:TypeA;ns:TypeB");
        result.ClrType.Should().Be("Some.Type, Assembly");
        result.Body.Should().Be("{\"key\":\"value\"}");
        result.ContentType.Should().Be("application/json");
        result.CorrelationId.Should().Be(CorrId);
        result.DispatcherContext.Should().ContainKey("ConversationId").WhoseValue.Should().Be(ConvId.ToString());
        result.DispatcherContext.Should().ContainKey("InitiatorId").WhoseValue.Should().Be(InitId.ToString());
        result.DispatcherContext.Should().ContainKey("RequestId").WhoseValue.Should().Be(ReqId.ToString());
        result.Headers.Should().ContainKey("X-Custom").WhoseValue.Should().Be("header-value");
        result.SentAt.Should().Be(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        result.CreatedAt.Should().Be(new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        result.DispatcherKind.Should().Be("MassTransit");
        result.DispatcherContext.Should().ContainKey("IsPublish").WhoseValue.Should().Be("true");
    }

    [Fact]
    public void FromMessage_IsProcessedTrue_MapsToTrue()
    {
        var result = DynamoDbOutboxMessage.FromMessage(new OutboxMessage { IsProcessed = true });

        result.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public void FromMessage_IsProcessedFalse_MapsToFalse()
    {
        var result = DynamoDbOutboxMessage.FromMessage(new OutboxMessage { IsProcessed = false });

        result.IsProcessed.Should().BeFalse();
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
            PK = MsgId,
            DestinationAddress = "queue://test",
            SourceAddress = "source://app",
            ResponseAddress = "response://queue",
            FaultAddress = "fault://queue",
            MessageType = "ns:TypeA",
            ClrType = "Some.Type, Assembly",
            Body = "{\"key\":\"value\"}",
            ContentType = "application/json",
            CorrelationId = CorrId,
            Headers = new Dictionary<string, object> { ["X-Custom"] = "header-value" },
            SentAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            IsProcessed = false,
            DispatcherKind = "MassTransit",
            DispatcherContext = new Dictionary<string, string> { ["IsPublish"] = "false" }
        };

        var result = dynamo.ToMessage();

        result.MessageId.Should().Be(MsgId);
        result.DestinationAddress.Should().Be("queue://test");
        result.SourceAddress.Should().Be("source://app");
        result.Body.Should().Be("{\"key\":\"value\"}");
        result.ContentType.Should().Be("application/json");
        result.DispatcherKind.Should().Be("MassTransit");
        result.DispatcherContext.Should().ContainKey("IsPublish").WhoseValue.Should().Be("false");
    }

    [Fact]
    public void ToMessage_IsProcessedTrue_MapsToTrue()
    {
        var result = new DynamoDbOutboxMessage { PK = MsgId, IsProcessed = true }.ToMessage();

        result.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public void ToMessage_IsProcessedFalse_MapsToFalse()
    {
        var result = new DynamoDbOutboxMessage { PK = MsgId, IsProcessed = false }.ToMessage();

        result.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var original = BuildFullMessage();

        var roundTripped = DynamoDbOutboxMessage.FromMessage(original).ToMessage();

        roundTripped.MessageId.Should().Be(original.MessageId);
        roundTripped.DestinationAddress.Should().Be(original.DestinationAddress);
        roundTripped.Body.Should().Be(original.Body);
        roundTripped.IsProcessed.Should().Be(original.IsProcessed);
        roundTripped.DispatcherKind.Should().Be(original.DispatcherKind);
        roundTripped.DispatcherContext.Should().BeEquivalentTo(original.DispatcherContext);
        roundTripped.Headers.Should().BeEquivalentTo(original.Headers);
        roundTripped.MessageType.Should().Be(original.MessageType);
    }
}