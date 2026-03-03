namespace HybridOutbox.Abstractions;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(
        TimeSpan processingThreshold,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default);

    Task<bool> TryAcquireLockAsync(
        string messageId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(string messageId, CancellationToken cancellationToken = default);
}
