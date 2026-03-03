namespace HybridOutbox.Internals;

public interface IInternalOutboxStore : IDisposable
{
    internal void Add(OutboxMessage message);
}