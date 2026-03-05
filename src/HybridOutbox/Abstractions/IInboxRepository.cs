namespace HybridOutbox.Abstractions;

public interface IInboxRepository
{
    Task<bool> ExistsAsync(Guid messageId, string consumerType, CancellationToken ct = default);
}