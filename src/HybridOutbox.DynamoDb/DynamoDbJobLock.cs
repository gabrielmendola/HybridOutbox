using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HybridOutbox.Abstractions;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb;

public sealed class DynamoDbJobLock : IOutboxJobLock
{
    private const string LockKey = "__JOB_LOCK__";
    private const string DateFormat = "O";

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly string _keyAttributeName;

    public DynamoDbJobLock(IAmazonDynamoDB dynamoDb, IOptions<DynamoDbOutboxOptions> options)
    {
        _dynamoDb = dynamoDb;
        var opts = options.Value;
        _tableName = opts.LockTableName ?? opts.TableName;
        _keyAttributeName = opts.LockTableName is not null ? "LockKey" : "MessageId";
    }

    public async Task<bool> TryAcquireAsync(string instanceId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.ToString(DateFormat);
        var expiresAt = DateTime.UtcNow.Add(leaseDuration).ToString(DateFormat);

        try
        {
            await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { _keyAttributeName, new AttributeValue { S = LockKey } }
                },
                UpdateExpression = "SET InstanceId = :instanceId, LeaseExpiresAt = :expiresAt",
                ConditionExpression =
                    "attribute_not_exists(LeaseExpiresAt) OR LeaseExpiresAt < :now OR InstanceId = :instanceId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":instanceId", new AttributeValue { S = instanceId } },
                    { ":expiresAt", new AttributeValue { S = expiresAt } },
                    { ":now", new AttributeValue { S = now } }
                }
            }, ct);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
