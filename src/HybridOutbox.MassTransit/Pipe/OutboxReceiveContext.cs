using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Context;

namespace HybridOutbox.MassTransit.Pipe;

public sealed class OutboxReceiveContext : ReceiveContextProxy
{
    public override IPublishEndpointProvider PublishEndpointProvider { get; }
    public override ISendEndpointProvider SendEndpointProvider { get; }

    public OutboxReceiveContext(ReceiveContext context, IOutboxStore store, OutboxDispatchContext dispatchContext)
        : base(context)
    {
        SendEndpointProvider = new OutboxSendEndpointProvider(context.SendEndpointProvider, store, dispatchContext);
        PublishEndpointProvider = new OutboxPublishEndpointProvider(context.PublishEndpointProvider, store, dispatchContext);
    }
}