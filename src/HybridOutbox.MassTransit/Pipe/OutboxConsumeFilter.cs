using HybridOutbox.MassTransit.Internals;
using MassTransit;
using MassTransit.DependencyInjection;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxConsumeFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly IConsumeScopeProvider _scopeProvider;
    private readonly string _consumerType;

    public OutboxConsumeFilter(IConsumeScopeProvider scopeProvider, string consumerType)
    {
        _scopeProvider = scopeProvider;
        _consumerType = consumerType;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        await using var scope = await _scopeProvider.GetScope(context).ConfigureAwait(false);

        var contextFactory = scope.GetService<IOutboxContextFactory>();

        await contextFactory.Send(scope.Context, next, _consumerType).ConfigureAwait(false);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("outbox");
    }
}