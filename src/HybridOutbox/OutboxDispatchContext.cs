namespace HybridOutbox;

public sealed class OutboxDispatchContext
{
    private static readonly AsyncLocal<bool> _isDispatching = new();

    public bool IsOutboxDispatching => _isDispatching.Value;

    public IDisposable BeginDispatch()
    {
        _isDispatching.Value = true;
        return new DispatchScope();
    }

    private sealed class DispatchScope : IDisposable
    {
        public void Dispose()
        {
            _isDispatching.Value = false;
        }
    }
}