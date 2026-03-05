using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HybridOutbox.Abstractions;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb.Internals;

internal sealed class DynamoDbJobLock : IOutboxJobLock
{
    private const string LockKey = "__JOB_LOCK__";
    private const string LockSk = "LOCK";
    private const string DateFormat = "O";

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbJobLock(IAmazonDynamoDB dynamoDb, IOptions<DynamoDbLockOptions> options)
    {
        _dynamoDb = dynamoDb;
        _tableName = options.Value.TableName;
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
                    { "PK", new AttributeValue { S = LockKey } },
                    { "SK", new AttributeValue { S = LockSk } }
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