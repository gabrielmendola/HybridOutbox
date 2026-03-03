using Amazon.DynamoDBv2.DataModel;

namespace Sample.WebApi.Entities;

[DynamoDBTable("AuditLogs")]
public class AuditLog
{
    [DynamoDBHashKey] public string AuditId { get; set; } = string.Empty;
    [DynamoDBProperty] public string Event { get; set; } = string.Empty;
    [DynamoDBProperty] public string OrderId { get; set; } = string.Empty;
    [DynamoDBProperty] public DateTime RecordedAt { get; set; }
}
