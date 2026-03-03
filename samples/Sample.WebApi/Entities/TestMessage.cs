using Amazon.DynamoDBv2.DataModel;

namespace Sample.WebApi.Entities;

[DynamoDBTable("TestMessages")]
public class TestMessage
{
    [DynamoDBHashKey] public string Id { get; set; } = string.Empty;

    [DynamoDBProperty] public string Text { get; set; } = string.Empty;

    [DynamoDBProperty] public DateTime Timestamp { get; set; }

    [DynamoDBProperty] public DateTime CreatedAt { get; set; }
}