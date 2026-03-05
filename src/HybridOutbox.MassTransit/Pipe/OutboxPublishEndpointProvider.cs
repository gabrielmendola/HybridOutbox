using HybridOutbox.Abstractions;
using MassTransit;

namespace HybridOutbox.MassTransit.Pipe;

internal sealed class OutboxPublishEndpointProvider : IPublishEndpointProvider
{
    private readonly IPublishEndpointProvider _publishEndpointProvider;
    private readonly IServiceProvider _provider;
    private readonly IOutboxContext _outboxContext;

    internal OutboxPublishEndpointProvider(
        IPublishEndpointProvider publishEndpointProvider,
        IServiceProvider provider,
        IOutboxContext outboxContext)
    {
        _publishEndpointProvider = publishEndpointProvider;
        _provider = provider;
        _outboxContext = outboxContext;
    }

    public async Task<ISendEndpoint> GetPublishSendEndpoint<T>() where T : class
    {
        var endpoint = await _publishEndpointProvider.GetPublishSendEndpoint<T>().ConfigureAwait(false);
        return new OutboxSendEndpoint(endpoint, _provider, _outboxContext);
    }

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
    {
        return _publishEndpointProvider.ConnectPublishObserver(observer);
    }
}