using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.DependencyInjection;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxScopedBusContextProvider<TBus> : IScopedBusContextProvider<TBus>
    where TBus : class, IBus
{
    public ScopedBusContext Context { get; }

    public OutboxScopedBusContextProvider(
        TBus bus,
        Bind<TBus, IClientFactory> clientFactory,
        Bind<TBus, IScopedConsumeContextProvider> consumeContextProvider,
        IScopedConsumeContextProvider globalConsumeContextProvider,
        IOutboxContext store,
        IServiceProvider serviceProvider)
    {
        if (consumeContextProvider.Value.HasContext)
            Context = new ConsumeContextScopedBusContext(
                consumeContextProvider.Value.GetContext(),
                clientFactory.Value);
        else if (globalConsumeContextProvider.HasContext)
            Context = new OutboxConsumeContextScopedBusContext<TBus>(
                bus,
                clientFactory.Value,
                serviceProvider,
                globalConsumeContextProvider.GetContext(),
                store);
        else
            Context = new OutboxScopedBusContext<TBus>(
                bus,
                clientFactory.Value,
                serviceProvider,
                store);
    }
}