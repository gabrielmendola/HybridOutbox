namespace Sample.WebApi.Messages;

public record TestCommand
{
    public Guid Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}