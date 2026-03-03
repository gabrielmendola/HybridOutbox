using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbTableInitializerTests
{
    private readonly IAmazonDynamoDB _dynamoDb = Substitute.For<IAmazonDynamoDB>();
    private const string TableName = "TestOutboxMessages";

    private DynamoDbTableInitializer CreateInitializer(TimeSpan? retentionPeriod) =>
        new(_dynamoDb,
            Options.Create(new DynamoDbOutboxOptions
            {
                TableName = TableName,
                RetentionPeriod = retentionPeriod,
                TtlAttributeName = "ExpiresAt"
            }),
            Substitute.For<ILogger<DynamoDbTableInitializer>>());

    private void SetupTableExists()
    {
        _dynamoDb.ListTablesAsync(Arg.Any<CancellationToken>())
            .Returns(new ListTablesResponse { TableNames = [TableName] });
    }

    private void SetupTableDoesNotExist()
    {
        _dynamoDb.ListTablesAsync(Arg.Any<CancellationToken>())
            .Returns(new ListTablesResponse { TableNames = [] });

        _dynamoDb.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateTableResponse());

        _dynamoDb.DescribeTableAsync(TableName, Arg.Any<CancellationToken>())
            .Returns(new DescribeTableResponse
            {
                Table = new TableDescription { TableStatus = TableStatus.ACTIVE }
            });

        _dynamoDb.UpdateTimeToLiveAsync(Arg.Any<UpdateTimeToLiveRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateTimeToLiveResponse());
    }

    [Fact]
    public async Task InitializeAsync_DoesNotCreateTable_WhenAlreadyExists()
    {
        SetupTableExists();

        await CreateInitializer(TimeSpan.FromDays(7)).InitializeAsync();

        await _dynamoDb.DidNotReceive()
            .CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_CreatesTable_WhenNotExists()
    {
        SetupTableDoesNotExist();

        await CreateInitializer(TimeSpan.FromDays(7)).InitializeAsync();

        await _dynamoDb.Received(1).CreateTableAsync(
            Arg.Is<CreateTableRequest>(r => r.TableName == TableName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_EnablesTtl_WhenRetentionPeriodSet()
    {
        SetupTableDoesNotExist();

        await CreateInitializer(TimeSpan.FromDays(7)).InitializeAsync();

        await _dynamoDb.Received(1).UpdateTimeToLiveAsync(
            Arg.Is<UpdateTimeToLiveRequest>(r =>
                r.TableName == TableName &&
                r.TimeToLiveSpecification.AttributeName == "ExpiresAt" &&
                r.TimeToLiveSpecification.Enabled == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_SkipsTtl_WhenNoRetentionPeriod()
    {
        SetupTableDoesNotExist();

        await CreateInitializer(retentionPeriod: null).InitializeAsync();

        await _dynamoDb.DidNotReceive()
            .UpdateTimeToLiveAsync(Arg.Any<UpdateTimeToLiveRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_RethrowsException_WhenListTablesFails()
    {
        _dynamoDb.ListTablesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonDynamoDBException("Service unavailable"));

        var act = () => CreateInitializer(null).InitializeAsync();

        await act.Should().ThrowAsync<AmazonDynamoDBException>();
    }
}
