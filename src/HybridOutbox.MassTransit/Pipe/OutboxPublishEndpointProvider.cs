using HybridOutbox.Abstractions;
using MassTransit;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxPublishEndpointProvider : IPublishEndpointProvider
{
    private readonly IPublishEndpointProvider _publishEndpointProvider;
    private readonly IOutboxStore _store;
    private readonly OutboxDispatchContext _dispatchContext;

    internal OutboxPublishEndpointProvider(
        IPublishEndpointProvider publishEndpointProvider,
        IOutboxStore store,
        OutboxDispatchContext dispatchContext)
    {
        _publishEndpointProvider = publishEndpointProvider;
        _store = store;
        _dispatchContext = dispatchContext;
    }

    public async Task<ISendEndpoint> GetPublishSendEndpoint<T>() where T : class
    {
        var endpoint = await _publishEndpointProvider.GetPublishSendEndpoint<T>().ConfigureAwait(false);
        return new OutboxSendEndpoint(endpoint, _store, _dispatchContext, isPublish: true);
    }

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
    {
        return _publishEndpointProvider.ConnectPublishObserver(observer);
    }
}