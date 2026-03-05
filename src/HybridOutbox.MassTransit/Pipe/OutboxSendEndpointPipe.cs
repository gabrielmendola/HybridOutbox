using MassTransit;
using MassTransit.Transports;

namespace HybridOutbox.MassTransit.Internals;

public class OutboxSendEndpointPipe<T> : SendContextPipeAdapter<T>
    where T : class
{
    private readonly IServiceProvider _provider;

    public OutboxSendEndpointPipe(IServiceProvider provider)
        : base(null)
    {
        _provider = provider;
    }

    public OutboxSendEndpointPipe(
        IPipe<SendContext<T>> pipe,
        IServiceProvider provider)
        : base(pipe)
    {
        _provider = provider;
    }

    protected override void Send<TMessage>(SendContext<TMessage> context)
    {
        context.GetOrAddPayload(() => _provider);
    }

    protected override void Send(SendContext<T> context)
    {
        context.ConversationId ??= NewId.NextGuid();
    }
}