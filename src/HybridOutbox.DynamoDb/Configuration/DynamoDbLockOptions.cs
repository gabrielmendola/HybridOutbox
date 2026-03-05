namespace HybridOutbox.DynamoDb.Configuration;

public sealed class DynamoDbLockOptions
{
    public const string SectionName = "HybridOutbox:DynamoDbLock";

    public string TableName { get; set; } = "HybridOutbox";
}