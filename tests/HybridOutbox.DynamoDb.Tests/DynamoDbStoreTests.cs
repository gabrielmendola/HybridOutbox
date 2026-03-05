using System.Threading.Channels;
using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbStoreTests
{
    private readonly IDynamoDBContext _context = Substitute.For<IDynamoDBContext>();

    private readonly ITransactWrite<DynamoDbOutboxMessage> _transactWrite =
        Substitute.For<ITransactWrite<DynamoDbOutboxMessage>>();

    private DynamoDbOutboxContext CreateStore(DynamoDbOutboxOptions? options = null)
    {
        options ??= new DynamoDbOutboxOptions { TableName = "TestTable" };

        _context.CreateTransactWrite<DynamoDbOutboxMessage>(Arg.Any<TransactWriteConfig>())
            .Returns(_transactWrite);

        return new DynamoDbOutboxContext(
            _context,
            Options.Create(options),
            Options.Create(new DynamoDbInboxOptions()),
            Channel.CreateUnbounded<OutboxMessage>().Writer,
            Substitute.For<ILogger<OutboxContext>>());
    }

    private List<DynamoDbOutboxMessage> CaptureAddSaveItemsArgument()
    {
        var captured = new List<DynamoDbOutboxMessage>();
        _transactWrite
            .When(x => x.AddSaveItems(Arg.Any<IEnumerable<DynamoDbOutboxMessage>>()))
            .Do(ci => captured.AddRange(ci.ArgAt<IEnumerable<DynamoDbOutboxMessage>>(0)));
        return captured;
    }

    private static readonly Guid MsgId1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MsgId2 = new("00000000-0000-0000-0000-000000000002");

    private static OutboxMessage SampleMessage(Guid? id = null)
    {
        return new OutboxMessage
        {
            MessageId = id ?? Guid.NewGuid(),
            DestinationAddress = "queue://test",
            Body = "{}",
            ContentType = "application/json"
        };
    }

    [Fact]
    public void GetTransactWrite_SetsExpiresAt_WhenRetentionPeriodConfigured()
    {
        var store = CreateStore(new DynamoDbOutboxOptions
        {
            TableName = "TestTable",
            RetentionPeriod = TimeSpan.FromDays(7)
        });
        store.Add(SampleMessage());

        var captured = CaptureAddSaveItemsArgument();
        var before = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(7)).ToUnixTimeSeconds();
        store.GetTransactWrite();
        var after = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(7)).ToUnixTimeSeconds();

        captured.Should().ContainSingle(m =>
            m.ExpiresAt.HasValue &&
            m.ExpiresAt.Value >= before &&
            m.ExpiresAt.Value <= after);
    }

    [Fact]
    public void GetTransactWrite_LeavesExpiresAtNull_WhenNoRetentionPeriod()
    {
        var store = CreateStore(new DynamoDbOutboxOptions
        {
            TableName = "TestTable",
            RetentionPeriod = null
        });
        store.Add(SampleMessage());

        var captured = CaptureAddSaveItemsArgument();
        store.GetTransactWrite();

        captured.Should().ContainSingle(m => !m.ExpiresAt.HasValue);
    }

    [Fact]
    public void GetTransactWrite_AddsOneRecordPerPendingMessage()
    {
        var store = CreateStore();
        store.Add(SampleMessage(MsgId1));
        store.Add(SampleMessage(MsgId2));

        var captured = CaptureAddSaveItemsArgument();
        store.GetTransactWrite();

        captured.Should().HaveCount(2);
        captured.Should().Contain(m => m.PK == MsgId1);
        captured.Should().Contain(m => m.PK == MsgId2);
    }

    [Fact]
    public void GetTransactWrite_ReturnsOnlyOutboxWrite_WhenNoInboxStaged()
    {
        var store = CreateStore();
        store.Add(SampleMessage());

        var result = store.GetTransactWrite();

        result.Should().HaveCount(1);
        result[0].Should().BeSameAs(_transactWrite);
    }
}