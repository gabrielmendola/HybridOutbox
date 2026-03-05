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

public sealed class DynamoDbInboxStoreTests
{
    private readonly IDynamoDBContext _context = Substitute.For<IDynamoDBContext>();

    private readonly ITransactWrite<DynamoDbOutboxMessage> _outboxTransactWrite =
        Substitute.For<ITransactWrite<DynamoDbOutboxMessage>>();

    private readonly ITransactWrite<DynamoDbInboxMessage> _inboxTransactWrite =
        Substitute.For<ITransactWrite<DynamoDbInboxMessage>>();

    private DynamoDbOutboxContext CreateStore(DynamoDbInboxOptions? options = null)
    {
        options ??= new DynamoDbInboxOptions { TableName = "TestInboxMessages" };

        _context.CreateTransactWrite<DynamoDbOutboxMessage>(Arg.Any<TransactWriteConfig>())
            .Returns(_outboxTransactWrite);
        _context.CreateTransactWrite<DynamoDbInboxMessage>(Arg.Any<TransactWriteConfig>())
            .Returns(_inboxTransactWrite);

        return new DynamoDbOutboxContext(
            _context,
            Options.Create(new DynamoDbOutboxOptions()),
            Options.Create(options),
            Channel.CreateUnbounded<OutboxMessage>().Writer,
            Substitute.For<ILogger<OutboxContext>>());
    }

    private static InboxMessage SampleMessage(Guid? id = null)
    {
        return new InboxMessage
        {
            MessageId = id ?? Guid.NewGuid(),
            ConsumerType = "Sample.Consumer",
            ProcessedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void GetTransactWrite_DoesNotAddInboxWrite_WhenNothingStaged()
    {
        var store = CreateStore();

        store.GetTransactWrite();

        _context.DidNotReceive().CreateTransactWrite<DynamoDbInboxMessage>(Arg.Any<TransactWriteConfig>());
    }

    [Fact]
    public void GetTransactWrite_IncludesInboxWrite_AfterAdd()
    {
        var store = CreateStore();
        store.Add(SampleMessage());

        var result = store.GetTransactWrite();

        result.Should().HaveCount(2);
        _inboxTransactWrite.Received(1).AddSaveItem(Arg.Any<DynamoDbInboxMessage>());
    }

    [Fact]
    public void GetTransactWrite_SetsExpiresAt_WhenRetentionPeriodConfigured()
    {
        var store = CreateStore(new DynamoDbInboxOptions
        {
            TableName = "TestInboxMessages",
            RetentionPeriod = TimeSpan.FromDays(7)
        });
        store.Add(SampleMessage());

        DynamoDbInboxMessage? captured = null;
        _inboxTransactWrite
            .When(x => x.AddSaveItem(Arg.Any<DynamoDbInboxMessage>()))
            .Do(ci => captured = ci.ArgAt<DynamoDbInboxMessage>(0));

        var before = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(7)).ToUnixTimeSeconds();
        store.GetTransactWrite();
        var after = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(7)).ToUnixTimeSeconds();

        captured!.ExpiresAt.Should().NotBeNull();
        captured.ExpiresAt!.Value.Should().BeInRange(before, after);
    }

    [Fact]
    public void GetTransactWrite_LeavesExpiresAtNull_WhenNoRetentionPeriod()
    {
        var store = CreateStore(new DynamoDbInboxOptions
        {
            TableName = "TestInboxMessages",
            RetentionPeriod = null
        });
        store.Add(SampleMessage());

        DynamoDbInboxMessage? captured = null;
        _inboxTransactWrite
            .When(x => x.AddSaveItem(Arg.Any<DynamoDbInboxMessage>()))
            .Do(ci => captured = ci.ArgAt<DynamoDbInboxMessage>(0));

        store.GetTransactWrite();

        captured!.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void GetTransactWrite_DoesNotAddInboxWrite_AfterDispose()
    {
        var store = CreateStore();
        store.Add(SampleMessage());
        store.Dispose();

        store.GetTransactWrite();

        _context.DidNotReceive().CreateTransactWrite<DynamoDbInboxMessage>(Arg.Any<TransactWriteConfig>());
    }
}