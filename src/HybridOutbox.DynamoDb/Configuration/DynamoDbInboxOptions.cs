namespace HybridOutbox.DynamoDb.Configuration;

public sealed class DynamoDbInboxOptions
{
    public const string SectionName = "HybridOutbox:DynamoDbInbox";
    public string TableName { get; set; } = "HybridOutbox";
    public TimeSpan? RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    public string TtlAttributeName { get; set; } = "ExpiresAt";
}