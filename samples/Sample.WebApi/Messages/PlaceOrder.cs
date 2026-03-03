namespace Sample.WebApi.Messages;

public record PlaceOrder
{
    public Guid OrderId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
