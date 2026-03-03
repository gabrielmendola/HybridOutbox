using HybridOutbox.Abstractions;
using MassTransit;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxSendEndpointProvider : ISendEndpointProvider
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IOutboxStore _store;
    private readonly OutboxDispatchContext _dispatchContext;

    internal OutboxSendEndpointProvider(
        ISendEndpointProvider sendEndpointProvider,
        IOutboxStore store,
        OutboxDispatchContext dispatchContext)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _store = store;
        _dispatchContext = dispatchContext;
    }

    public async Task<ISendEndpoint> GetSendEndpoint(Uri address)
    {
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(address).ConfigureAwait(false);
        return new OutboxSendEndpoint(endpoint, _store, _dispatchContext, address, false);
    }

    public ConnectHandle ConnectSendObserver(ISendObserver observer)
    {
        return _sendEndpointProvider.ConnectSendObserver(observer);
    }
}