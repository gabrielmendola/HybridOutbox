using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb;

public sealed class DynamoDbTableInitializer
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly DynamoDbOutboxOptions _options;
    private readonly ILogger<DynamoDbTableInitializer> _logger;

    public DynamoDbTableInitializer(
        IAmazonDynamoDB dynamoDb,
        IOptions<DynamoDbOutboxOptions> options,
        ILogger<DynamoDbTableInitializer> logger)
    {
        _dynamoDb = dynamoDb;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tables = await _dynamoDb.ListTablesAsync(cancellationToken);
            if (tables.TableNames.Contains(_options.TableName))
            {
                _logger.LogInformation("HybridOutbox DynamoDB table {TableName} already exists", _options.TableName);
                return;
            }

            _logger.LogInformation("Creating HybridOutbox DynamoDB table {TableName}", _options.TableName);

            await _dynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = _options.TableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new() { AttributeName = "PK", KeyType = KeyType.HASH },
                    new() { AttributeName = "SK", KeyType = KeyType.RANGE }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new() { AttributeName = "PK", AttributeType = ScalarAttributeType.S },
                    new() { AttributeName = "SK", AttributeType = ScalarAttributeType.S },
                    new() { AttributeName = "PendingMark", AttributeType = ScalarAttributeType.S },
                    new() { AttributeName = "CreatedAt", AttributeType = ScalarAttributeType.S }
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new()
                    {
                        IndexName = "PendingMark-CreatedAt-index",
                        KeySchema = new List<KeySchemaElement>
                        {
                            new() { AttributeName = "PendingMark", KeyType = KeyType.HASH },
                            new() { AttributeName = "CreatedAt", KeyType = KeyType.RANGE }
                        },
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            }, cancellationToken);

            _logger.LogInformation("HybridOutbox DynamoDB table {TableName} created successfully", _options.TableName);

            await WaitForActiveAsync(_options.TableName, cancellationToken);

            if (_options.RetentionPeriod.HasValue)
            {
                await _dynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = _options.TableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification
                    {
                        AttributeName = _options.TtlAttributeName,
                        Enabled = true
                    }
                }, cancellationToken);

                _logger.LogInformation(
                    "HybridOutbox DynamoDB TTL enabled on attribute '{TtlAttribute}' (retention={Retention})",
                    _options.TtlAttributeName, _options.RetentionPeriod.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing HybridOutbox DynamoDB table {TableName}", _options.TableName);
            throw;
        }
    }

    private async Task WaitForActiveAsync(string tableName, CancellationToken cancellationToken)
    {
        var tableStatus = TableStatus.CREATING;
        while (tableStatus == TableStatus.CREATING)
        {
            await Task.Delay(1000, cancellationToken);
            var describeResponse = await _dynamoDb.DescribeTableAsync(tableName, cancellationToken);
            tableStatus = describeResponse.Table.TableStatus;
        }

        _logger.LogInformation("HybridOutbox DynamoDB table {TableName} is now active", tableName);
    }
}