using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Context;

namespace HybridOutbox.MassTransit.Pipe;

public sealed class OutboxConsumeContext<TMessage> : ConsumeContextProxy<TMessage>
    where TMessage : class
{
    public OutboxConsumeContext(
        ConsumeContext<TMessage> context,
        IServiceProvider provider,
        IOutboxContext outboxContext)
        : base(context)
    {
        var outboxReceiveContext = new OutboxReceiveContext(context.ReceiveContext, provider, outboxContext);

        ReceiveContext = outboxReceiveContext;
        PublishEndpointProvider = outboxReceiveContext.PublishEndpointProvider;
    }
}