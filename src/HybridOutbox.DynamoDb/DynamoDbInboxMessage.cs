using Amazon.DynamoDBv2.DataModel;

namespace HybridOutbox.DynamoDb;

[DynamoDBTable("HybridOutbox")]
public sealed class DynamoDbInboxMessage
{
    private const string SortKeyPrefix = "INBOX#";

    public static string BuildSortKey(string consumerType)
    {
        return $"{SortKeyPrefix}{consumerType}";
    }

    [DynamoDBHashKey("PK")]
    public string PK { get; set; } = null!;

    [DynamoDBRangeKey("SK")]
    public string SK { get; set; } = null!;

    [DynamoDBProperty("ConsumerType")]
    public string ConsumerType { get; set; } = null!;

    [DynamoDBProperty("ProcessedAt")]
    public string? ProcessedAt { get; set; }

    [DynamoDBProperty("ExpiresAt")]
    public long? ExpiresAt { get; set; }
}