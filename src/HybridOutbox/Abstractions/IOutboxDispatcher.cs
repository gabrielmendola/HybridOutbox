using HybridOutbox.Internals;

namespace HybridOutbox.Abstractions;

public interface IOutboxDispatcher
{
    Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
