namespace HybridOutbox.Abstractions;

public interface IOutboxContext : IDisposable
{
    OutboxMessage[] GetUndispatchedMessages();
    void DispatchMessages();
    void Add(OutboxMessage message);
    void Add(InboxMessage message);
    void Clear();
}