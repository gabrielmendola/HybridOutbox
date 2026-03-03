using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using HybridOutbox.MassTransit.Tests.Helpers;
using MassTransit;
using NSubstitute;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxSendEndpointProviderTests
{
    private readonly ISendEndpointProvider _inner = Substitute.For<ISendEndpointProvider>();
    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly OutboxDispatchContext _dispatchContext = new();

    private OutboxSendEndpointProvider BuildProvider() =>
        new(_inner, _store, _dispatchContext);

    [Fact]
    public async Task GetSendEndpoint_ReturnsEndpointThatAddsToStore()
    {
        var innerEndpoint = Substitute.For<ISendEndpoint>();
        _inner.GetSendEndpoint(Arg.Any<Uri>()).Returns(innerEndpoint);

        var provider = BuildProvider();
        var endpoint = await provider.GetSendEndpoint(new Uri("rabbitmq://localhost/test-queue"));

        await endpoint.Send(new TestMessage());

        _store.Received(1).Add(Arg.Any<OutboxMessage>());
    }

    [Fact]
    public async Task GetSendEndpoint_PassesAddressToInner()
    {
        var address = new Uri("rabbitmq://localhost/my-queue");
        _inner.GetSendEndpoint(Arg.Any<Uri>()).Returns(Substitute.For<ISendEndpoint>());

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
