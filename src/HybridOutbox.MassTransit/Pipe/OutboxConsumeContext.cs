using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using MassTransit.Context;

namespace HybridOutbox.MassTransit;

public sealed class OutboxConsumeContext<TMessage> : ConsumeContextProxy<TMessage>
    where TMessage : class
{
    public OutboxConsumeContext(ConsumeContext<TMessage> context, IOutboxStore store, OutboxDispatchContext dispatchContext)
        : base(context)
    {
        var outboxReceiveContext = new OutboxReceiveContext(context.ReceiveContext, store, dispatchContext);
        
        ReceiveContext = outboxReceiveContext;
        PublishEndpointProvider = outboxReceiveContext.PublishEndpointProvider;
    }
}