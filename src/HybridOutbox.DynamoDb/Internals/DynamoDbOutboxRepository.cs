using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb.Internals;

internal sealed class DynamoDbOutboxRepository : IOutboxRepository
{
    private const string DateFormat = "O";

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IDynamoDBContext _context;
    private readonly DynamoDbOutboxOptions _dbOptions;
    private readonly OutboxOptions _options;
    private readonly ILogger<DynamoDbOutboxRepository> _logger;

    public DynamoDbOutboxRepository(
        IAmazonDynamoDB dynamoDb,
        IDynamoDBContext context,
        IOptions<DynamoDbOutboxOptions> dbOptions,
        IOptions<OutboxOptions> options,
        ILogger<DynamoDbOutboxRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _context = context;
        _dbOptions = dbOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(
        TimeSpan processingThreshold,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var threshold = Iso(DateTime.UtcNow - processingThreshold);
        var lockExpiry = Iso(DateTime.UtcNow - _options.Processing.Lock.Duration);

        var request = new QueryRequest
        {
            TableName = _dbOptions.TableName,
            IndexName = "PendingMark-CreatedAt-index",
            KeyConditionExpression = "PendingMark = :mark AND CreatedAt < :threshold",
            FilterExpression = "attribute_not_exists(LockedAt) OR LockedAt < :lockExpiry",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":mark", Str(DynamoDbOutboxMessage.SortKey) },
                { ":threshold", Str(threshold) },
                { ":lockExpiry", Str(lockExpiry) }
            },
            Limit = limit
        };

        var results = new List<OutboxMessage>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            if (lastKey is not null)
                request.ExclusiveStartKey = lastKey;

            var response = await _dynamoDb.QueryAsync(request, cancellationToken);

            foreach (var item in response.Items)
            {
                var record = _context.FromDocument<DynamoDbOutboxMessage>(Document.FromAttributeMap(item));
                results.Add(record.ToMessage());
            }

            lastKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null;
        } while (lastKey is not null && results.Count < limit);

        return results;
    }

    public async Task MarkAsProcessedAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _dbOptions.TableName,
                Key = MessageKey(messageId),
                UpdateExpression = "SET IsProcessed = :true, ProcessedAt = :now REMOVE PendingMark",
                ConditionExpression = "attribute_exists(PK)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":true", new AttributeValue { BOOL = true } },
                    { ":now", Str(Iso(DateTime.UtcNow)) }
                }
            }, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning("MarkAsProcessed: message {MessageId} not found", messageId);
        }
    }

    public async Task<bool> TryAcquireLockAsync(
        Guid messageId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        var lockExpiry = Iso(DateTime.UtcNow - lockDuration);

        try
        {
            await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _dbOptions.TableName,
                Key = MessageKey(messageId),
                UpdateExpression = "SET LockedAt = :now",
                ConditionExpression = "attribute_not_exists(LockedAt) OR LockedAt < :lockExpiry",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":now", Str(Iso(DateTime.UtcNow)) },
                    { ":lockExpiry", Str(lockExpiry) }
                }
            }, cancellationToken);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public Task ReleaseLockAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _dbOptions.TableName,
            Key = MessageKey(messageId),
            UpdateExpression = "REMOVE LockedAt"
        }, cancellationToken);
    }

    private static string Iso(DateTime dt)
    {
        return dt.ToUniversalTime().ToString(DateFormat);
    }

    private static AttributeValue Str(string value)
    {
        return new AttributeValue { S = value };
    }

    private static Dictionary<string, AttributeValue> MessageKey(Guid messageId)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = messageId.ToString("D") } },
            { "SK", new AttributeValue { S = DynamoDbOutboxMessage.SortKey } }
        };
    }
}