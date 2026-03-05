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
    private static readonly Guid MsgId = new("00000001-0000-0000-0000-000000000000");

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
            PK = MsgId,
            DestinationAddress = "queue://test",
            Body = "{}",
            ContentType = "application/json"
        };

        _dynamoDb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items =
                [
                    new Dictionary<string, AttributeValue> { { "PK", new AttributeValue { S = MsgId.ToString() } } }
                ],
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });

        _context.FromDocument<DynamoDbOutboxMessage>(Arg.Any<Document>())
            .Returns(dynamoRecord);

        var result = await _repository.GetUnprocessedAsync(TimeSpan.FromSeconds(10));

        result.Should().HaveCount(1);
        result[0].MessageId.Should().Be(MsgId);
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
                r.IndexName == "PendingMark-CreatedAt-index" &&
                r.KeyConditionExpression.Contains("PendingMark")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsProcessedAsync_SendsCorrectUpdateExpression()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        await _repository.MarkAsProcessedAsync(MsgId);

        await _dynamoDb.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(r =>
                r.TableName == _dbOptions.TableName &&
                r.UpdateExpression.Contains("IsProcessed") &&
                r.Key["PK"].S == MsgId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsProcessedAsync_DoesNotThrow_WhenConditionalCheckFails()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UpdateItemResponse>(new ConditionalCheckFailedException("Concurrent update")));

        var act = () => _repository.MarkAsProcessedAsync(MsgId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ReturnsTrue_WhenUpdateSucceeds()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        var result = await _repository.TryAcquireLockAsync(MsgId, TimeSpan.FromSeconds(30));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ReturnsFalse_WhenConditionalCheckFails()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UpdateItemResponse>(new ConditionalCheckFailedException("Already locked")));

        var result = await _repository.TryAcquireLockAsync(MsgId, TimeSpan.FromSeconds(30));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_SendsRemoveLockedAtExpression()
    {
        _dynamoDb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        await _repository.ReleaseLockAsync(MsgId);

        await _dynamoDb.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(r =>
                r.TableName == _dbOptions.TableName &&
                r.UpdateExpression == "REMOVE LockedAt" &&
                r.Key["PK"].S == MsgId.ToString() &&
                r.Key["SK"].S == "OUTBOX"),
            Arg.Any<CancellationToken>());
    }
}