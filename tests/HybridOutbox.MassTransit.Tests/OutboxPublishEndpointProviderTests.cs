using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using NSubstitute;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxPublishEndpointProviderTests
{
    private readonly IPublishEndpointProvider _inner = Substitute.For<IPublishEndpointProvider>();
    private readonly IOutboxContext _outboxContext = Substitute.For<IOutboxContext>();
    private readonly IServiceProvider _provider = Substitute.For<IServiceProvider>();

    private OutboxPublishEndpointProvider BuildProvider()
    {
        return new OutboxPublishEndpointProvider(_inner, _provider, _outboxContext);
    }

    [Fact]
    public void ConnectPublishObserver_DelegatesToInner()
    {
        var observer = Substitute.For<IPublishObserver>();

        BuildProvider().ConnectPublishObserver(observer);

        _inner.Received(1).ConnectPublishObserver(observer);
    }
}
