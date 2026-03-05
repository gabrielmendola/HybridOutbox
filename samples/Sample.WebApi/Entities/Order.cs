using Amazon.DynamoDBv2.DataModel;

namespace Sample.WebApi.Entities;

[DynamoDBTable("Orders")]
public class Order
{
    [DynamoDBHashKey] public string OrderId { get; set; } = string.Empty;
    [DynamoDBProperty] public string ProductName { get; set; } = string.Empty;
    [DynamoDBProperty] public int Quantity { get; set; }
    [DynamoDBProperty] public DateTime PlacedAt { get; set; }
}