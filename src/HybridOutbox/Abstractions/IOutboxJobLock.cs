namespace HybridOutbox.Abstractions;

public interface IOutboxJobLock
{
    Task<bool> TryAcquireAsync(string instanceId, TimeSpan leaseDuration, CancellationToken ct = default);
}