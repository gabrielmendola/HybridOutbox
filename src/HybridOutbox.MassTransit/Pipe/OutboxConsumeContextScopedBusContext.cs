using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Clients;
using MassTransit.DependencyInjection;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxConsumeContextScopedBusContext<TBus> : OutboxScopedBusContext<TBus>
    where TBus : class, IBus
{
    private readonly TBus _bus;
    private readonly IClientFactory _clientFactory;
    private readonly ConsumeContext _consumeContext;
    private readonly IServiceProvider _provider;

    internal OutboxConsumeContextScopedBusContext(
        TBus bus,
        IClientFactory clientFactory,
        IServiceProvider provider,
        ConsumeContext consumeContext,
        IOutboxContext outboxContext)
        : base(bus, clientFactory, provider, outboxContext)
    {
        _bus = bus;
        _clientFactory = clientFactory;
        _provider = provider;
        _consumeContext = consumeContext;
    }

    protected override IPublishEndpointProvider GetPublishEndpointProvider()
    {
        return new ScopedConsumePublishEndpointProvider(_bus, _consumeContext, _provider);
    }

    protected override ISendEndpointProvider GetSendEndpointProvider()
    {
        return new ScopedConsumeSendEndpointProvider(_bus, _consumeContext, _provider);
    }

    protected override ScopedClientFactory GetClientFactory()
    {
        return new ScopedClientFactory(new ClientFactory(new ScopedClientFactoryContext(_clientFactory, _provider)),
            _consumeContext);
    }
}