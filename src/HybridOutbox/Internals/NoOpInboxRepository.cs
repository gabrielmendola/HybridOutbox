using HybridOutbox.Abstractions;

namespace HybridOutbox.Internals;

internal sealed class NoOpInboxRepository : IInboxRepository
{
    public Task<bool> ExistsAsync(Guid messageId, string consumerType, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }
}