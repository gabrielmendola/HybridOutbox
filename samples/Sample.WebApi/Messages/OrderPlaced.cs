namespace Sample.WebApi.Messages;

public record OrderPlaced
{
    public Guid OrderId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public DateTime PlacedAt { get; init; }
}