using HybridOutbox.Internals;

namespace HybridOutbox.Abstractions;

public interface IOutboxStore : IInternalOutboxStore
{
    OutboxMessage[] GetUndispatchedMessages();
    void DispatchMessages();
    void Clear();
}