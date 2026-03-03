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
            await InitializeOutboxTableAsync(cancellationToken);

            if (_options.LockTableName is not null)
                await InitializeLockTableAsync(_options.LockTableName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing HybridOutbox DynamoDB table {TableName}", _options.TableName);
            throw;
        }
    }

    private async Task InitializeOutboxTableAsync(CancellationToken cancellationToken)
    {
        var tables = await _dynamoDb.ListTablesAsync(cancellationToken);
        if (tables.TableNames.Contains(_options.TableName))
        {
            _logger.LogInformation("HybridOutbox DynamoDB table {TableName} already exists", _options.TableName);
            return;
        }

        _logger.LogInformation("Creating HybridOutbox DynamoDB table {TableName}", _options.TableName);

        var request = new CreateTableRequest
        {
            TableName = _options.TableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "MessageId", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "MessageId", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "IsProcessed", AttributeType = ScalarAttributeType.N },
                new() { AttributeName = "CreatedAt", AttributeType = ScalarAttributeType.S }
            },
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new()
                {
                    IndexName = _options.GsiName,
                    KeySchema = new List<KeySchemaElement>
                    {
                        new() { AttributeName = "IsProcessed", KeyType = KeyType.HASH },
                        new() { AttributeName = "CreatedAt", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await _dynamoDb.CreateTableAsync(request, cancellationToken);
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

    private async Task InitializeLockTableAsync(string tableName, CancellationToken cancellationToken)
    {
        var tables = await _dynamoDb.ListTablesAsync(cancellationToken);
        if (tables.TableNames.Contains(tableName))
        {
            _logger.LogInformation("HybridOutbox lock table {TableName} already exists", tableName);
            return;
        }

        _logger.LogInformation("Creating HybridOutbox lock table {TableName}", tableName);

        await _dynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "LockKey", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "LockKey", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        }, cancellationToken);

        _logger.LogInformation("HybridOutbox lock table {TableName} created successfully", tableName);

        await WaitForActiveAsync(tableName, cancellationToken);

        _logger.LogInformation("HybridOutbox lock table {TableName} is now active", tableName);
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
