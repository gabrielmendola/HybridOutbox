using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Context;

namespace HybridOutbox.MassTransit.Pipe;

public sealed class OutboxReceiveContext : ReceiveContextProxy
{
    public override IPublishEndpointProvider PublishEndpointProvider { get; }
    public override ISendEndpointProvider SendEndpointProvider { get; }

    public OutboxReceiveContext(
        ReceiveContext context,
        IServiceProvider provider,
        IOutboxContext outboxContext)
        : base(context)
    {
        SendEndpointProvider = new OutboxSendEndpointProvider(context.SendEndpointProvider, provider, outboxContext);
        PublishEndpointProvider = new OutboxPublishEndpointProvider(context.PublishEndpointProvider, provider, outboxContext);
    }
}