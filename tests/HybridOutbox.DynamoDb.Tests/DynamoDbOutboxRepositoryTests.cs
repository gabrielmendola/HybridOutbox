using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using HybridOutbox.Configuration;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbOutboxRepositoryTests
{
    private readonly IAmazonDynamoDB _dynamoDb = Substitute.For<IAmazonDynamoDB>();
    private readonly IDynamoDBContext _context = Substitute.For<IDynamoDBContext>();
    private readonly DynamoDbOutboxOptions _dbOptions = new()
    {
        TableName = "TestOutboxMessages",
        GsiName = "IsProcessed-CreatedAt-index"
    };
    private readonly DynamoDbOutboxRepository _repository;

    public DynamoDbOutboxRepositoryTests()
    {
        _repository = new DynamoDbOutboxRepository(
            _dynamoDb,
            _context,
            Options.Create(_dbOptions),
            Options.Create(new OutboxOptions()),
            Substitute.For<ILogger<DynamoDbOutboxRepository>>());
    }

    [Fact]
    public async Task GetUnprocessedAsync_ReturnsMessages_WhenQueryReturnsItems()
    {
        var dynamoRecord = new DynamoDbOutboxMessage
        {
            MessageId = "msg-1",
            DestinationAddress = "queue://test",
            Body = "{}",
            ContentType = "application/json"
        };

        _dynamoDb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue> { { "MessageId", new AttributeValue { S = "msg-1" } } }],
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });

        _context.FromDocument<DynamoDbOutboxMessage>(Arg.Any<Document>())
            .Returns(dynamoRecord);

        var result = await _repository.GetUnprocessedAsync(TimeSpan.FromSeconds(10));

        result.Should().HaveCount(1);
        result[0].MessageId.Should().Be("msg-1");
    }

    [Fact]
    public async Task GetUnprocessedAsync_ReturnsEmpty_WhenQueryReturnsNoItems()
    {
        _dynamoDb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [],
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });

        var result = await _repository.GetUnprocessedAsync(TimeSpan.FromSeconds(10));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnprocessedAsync_UsesCorrectGsiAndCondition()
    {
        _dynamoDb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [],
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });

        await _repository.GetUnprocessedAsync(TimeSpan.FromSeconds(10));

        await _dynamoDb.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.TableName == _dbOptions.TableName &&
                r.IndexName == _dbOptions.GsiName &&
                r.KeyConditionExpression.Contains("IsProcessed")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsProcessedAsync_SetsIsProcessedAndSaves_WhenFound()
    {
        var record = new DynamoDbOutboxMessage { MessageId = "msg-1", IsProcessed = 0 };

        _context.LoadAsync<DynamoDbOutboxMessage>(
                Arg.Any<object>(), Arg.Any<LoadConfig>(), Arg.Any<CancellationToken>())
            .Returns(record);

        await _repository.MarkAsProcessedAsync("msg-1");

        record.IsProcessed.Should().Be(1);
        record.ProcessedAt.Should().NotBeNull();
        await _context.Received(1).SaveAsync(record, Arg.Any<SaveConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsProcessedAsync_DoesNotSave_WhenMessageNotFound()
    {
        _context.LoadAsync<DynamoDbOutboxMessage>(
                Arg.Any<object>(), Arg.Any<LoadConfig>(), Arg.Any<CancellationToken>())
            .Returns((DynamoDbOutboxMessage)null!);

        await _repository.MarkAsProcessedAsync("missing-id");

        await _context.DidNotReceive()
            .SaveAsync(Arg.Any<DynamoDbOutboxMessage>(), Arg.Any<SaveConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsProcessedAsync_DoesNotThrow_WhenConditionalCheckFails()
    {
        var record = new DynamoDbOutboxMessage { MessageId = "msg-1" };

        _context.LoadAsync<DynamoDbOutboxMessage>(
                Arg.Any<object>(), Arg.Any<LoadConfig>(), Arg.Any<CancellationToken>())
            .Returns(record);

        _context.SaveAsync(record, Arg.Any<SaveConfig>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConditionalCheckFailedException("Concurrent update")));

        var act = () => _repository.MarkAsProcessedAsync("msg-1");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ReturnsTrue_WhenUpdateSucceeds()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        var result = await _repository.TryAcquireLockAsync("msg-1", TimeSpan.FromSeconds(30));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ReturnsFalse_WhenConditionalCheckFails()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UpdateItemResponse>(new ConditionalCheckFailedException("Already locked")));

        var result = await _repository.TryAcquireLockAsync("msg-1", TimeSpan.FromSeconds(30));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_SendsRemoveLockedAtExpression()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        await _repository.ReleaseLockAsync("msg-1");

        await _dynamoDb.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(r =>
                r.TableName == _dbOptions.TableName &&
                r.UpdateExpression == "REMOVE LockedAt" &&
                r.Key["MessageId"].S == "msg-1"),
            Arg.Any<CancellationToken>());
    }
}
