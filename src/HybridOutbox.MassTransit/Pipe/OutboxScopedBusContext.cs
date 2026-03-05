using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Clients;
using MassTransit.DependencyInjection;
using MassTransit.Transports;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxScopedBusContext<TBus> :
    ScopedBusContext
    where TBus : class, IBus
{
    private readonly TBus _bus;
    private readonly IClientFactory _clientFactory;
    private readonly IServiceProvider _provider;
    private readonly IOutboxContext _outboxContext;

    private IPublishEndpoint? _publishEndpoint;
    private ISendEndpointProvider? _sendEndpointProvider;

    internal OutboxScopedBusContext(
        TBus bus,
        IClientFactory clientFactory,
        IServiceProvider provider,
        IOutboxContext outboxContext)
    {
        _bus = bus;
        _clientFactory = clientFactory;
        _provider = provider;
        _outboxContext = outboxContext;
    }

    public ISendEndpointProvider SendEndpointProvider => _sendEndpointProvider ??=
        new OutboxSendEndpointProvider(GetSendEndpointProvider(), _provider, _outboxContext);

    public IPublishEndpoint PublishEndpoint => _publishEndpoint ??=
        new PublishEndpoint(new OutboxPublishEndpointProvider(GetPublishEndpointProvider(), _provider, _outboxContext));

    public IScopedClientFactory ClientFactory => GetClientFactory();

    protected virtual ScopedClientFactory GetClientFactory()
    {
        return new ScopedClientFactory(new ClientFactory(new ScopedClientFactoryContext(_clientFactory, _provider)),
            null);
    }

    protected virtual IPublishEndpointProvider GetPublishEndpointProvider()
    {
        return _bus;
    }

    protected virtual ISendEndpointProvider GetSendEndpointProvider()
    {
        return _bus;
    }
}