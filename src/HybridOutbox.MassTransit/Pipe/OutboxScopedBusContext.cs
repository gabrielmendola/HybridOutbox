using HybridOutbox.Abstractions;
using MassTransit;
using MassTransit.Clients;
using MassTransit.DependencyInjection;
using MassTransit.Transports;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxScopedBusContext<TBus> : ScopedBusContext
    where TBus : class, IBus
{
    private readonly TBus _bus;
    private readonly IClientFactory _clientFactory;
    private readonly IServiceProvider _provider;
    private readonly IOutboxStore _store;
    private readonly OutboxDispatchContext _dispatchContext;

    private IPublishEndpoint? _publishEndpoint;
    private ISendEndpointProvider? _sendEndpointProvider;

    internal OutboxScopedBusContext(
        TBus bus,
        IClientFactory clientFactory,
        IServiceProvider provider,
        IOutboxStore store,
        OutboxDispatchContext dispatchContext)
    {
        _bus = bus;
        _clientFactory = clientFactory;
        _provider = provider;
        _store = store;
        _dispatchContext = dispatchContext;
    }

    public ISendEndpointProvider SendEndpointProvider => _sendEndpointProvider ??=
        new OutboxSendEndpointProvider(GetSendEndpointProvider(), _store, _dispatchContext);

    public IPublishEndpoint PublishEndpoint => _publishEndpoint ??=
        new PublishEndpoint(new OutboxPublishEndpointProvider(GetPublishEndpointProvider(), _store, _dispatchContext));

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