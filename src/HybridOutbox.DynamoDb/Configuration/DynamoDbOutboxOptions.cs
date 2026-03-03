namespace HybridOutbox.DynamoDb.Configuration;

public class DynamoDbOutboxOptions
{
    public const string SectionName = "HybridOutbox:DynamoDb";

    public string TableName { get; set; } = "OutboxMessages";
    public string? LockTableName { get; set; }
    public string GsiName { get; set; } = "IsProcessed-CreatedAt-index";

    public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    public string TtlAttributeName { get; set; } = "ExpiresAt";
}
