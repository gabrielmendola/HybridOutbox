using HybridOutbox.Abstractions;
using MassTransit;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxSendEndpointProvider : ISendEndpointProvider
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IServiceProvider _provider;
    private readonly IOutboxContext _outboxContext;

    internal OutboxSendEndpointProvider(
        ISendEndpointProvider sendEndpointProvider,
        IServiceProvider provider,
        IOutboxContext outboxContext)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _provider = provider;
        _outboxContext = outboxContext;
    }

    public async Task<ISendEndpoint> GetSendEndpoint(Uri address)
    {
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(address).ConfigureAwait(false);
        return new OutboxSendEndpoint(endpoint, _provider, _outboxContext);
    }

    public ConnectHandle ConnectSendObserver(ISendObserver observer)
    {
        return _sendEndpointProvider.ConnectSendObserver(observer);
    }
}