using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using MassTransit.Transports;
using NSubstitute;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxSendEndpointProviderTests
{
    private readonly ISendEndpointProvider _inner = Substitute.For<ISendEndpointProvider>();
    private readonly IOutboxContext _outboxContext = Substitute.For<IOutboxContext>();
    private readonly IServiceProvider _provider = Substitute.For<IServiceProvider>();

    private OutboxSendEndpointProvider BuildProvider()
    {
        return new OutboxSendEndpointProvider(_inner, _provider, _outboxContext);
    }

    [Fact]
    public async Task GetSendEndpoint_PassesAddressToInner()
    {
        var address = new Uri("rabbitmq://localhost/my-queue");
        _inner.GetSendEndpoint(Arg.Any<Uri>()).Returns(Substitute.For<ITransportSendEndpoint>());

        await BuildProvider().GetSendEndpoint(address);

        await _inner.Received(1).GetSendEndpoint(address);
    }

    [Fact]
    public void ConnectSendObserver_DelegatesToInner()
    {
        var observer = Substitute.For<ISendObserver>();

        BuildProvider().ConnectSendObserver(observer);

        _inner.Received(1).ConnectSendObserver(observer);
    }
}
