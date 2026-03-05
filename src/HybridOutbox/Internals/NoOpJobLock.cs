using HybridOutbox.Abstractions;

namespace HybridOutbox.Internals;

internal sealed class NoOpJobLock : IOutboxJobLock
{
    public Task<bool> TryAcquireAsync(string instanceId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}