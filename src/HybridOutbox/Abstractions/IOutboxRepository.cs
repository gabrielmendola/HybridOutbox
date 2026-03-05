namespace HybridOutbox.Abstractions;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(
        TimeSpan processingThreshold,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<bool> TryAcquireLockAsync(
        Guid messageId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(Guid messageId, CancellationToken cancellationToken = default);
}