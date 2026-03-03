
using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.DependencyInjection;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxConsumeFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly IConsumeScopeProvider _scopeProvider;

    public OutboxConsumeFilter(IConsumeScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }
    
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        await using IConsumeScopeContext<TMessage> scope = await _scopeProvider.GetScope(context).ConfigureAwait(false);

        var store = scope.GetService<IOutboxStore>();
        var dispatchContext = scope.GetService<OutboxDispatchContext>();
        var outboxContext = new OutboxConsumeContext<TMessage>(scope.Context, store, dispatchContext);
        
        await next.Send(outboxContext).ConfigureAwait(false);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("outbox");
    }
}