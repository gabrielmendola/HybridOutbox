using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbInboxRepositoryTests
{
    private readonly IDynamoDBContext _context = Substitute.For<IDynamoDBContext>();

    private DynamoDbInboxRepository CreateRepository(string tableName = "TestInboxMessages")
    {
        var options = new DynamoDbInboxOptions { TableName = tableName };
        return new DynamoDbInboxRepository(_context, Options.Create(options));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenRecordFound()
    {
        var record = new DynamoDbInboxMessage
        {
            PK = "msg-1",
            ConsumerType = "Sample.Consumer"
        };

        _context.LoadAsync<DynamoDbInboxMessage>(
                Arg.Any<object>(), Arg.Any<object>(), Arg.Any<LoadConfig>(), Arg.Any<CancellationToken>())
            .Returns(record);

        var repository = CreateRepository();
        var result = await repository.ExistsAsync(new Guid("00000001-0000-0000-0000-000000000000"), "Sample.Consumer");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenRecordNotFound()
    {
        _context.LoadAsync<DynamoDbInboxMessage>(
                Arg.Any<object>(), Arg.Any<object>(), Arg.Any<LoadConfig>(), Arg.Any<CancellationToken>())
            .Returns((DynamoDbInboxMessage)null!);

        var repository = CreateRepository();
        var result = await repository.ExistsAsync(Guid.NewGuid(), "Sample.Consumer");

        result.Should().BeFalse();
    }
}