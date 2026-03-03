using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using HybridOutbox.MassTransit.Tests.Helpers;
using MassTransit;
using NSubstitute;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxPublishEndpointProviderTests
{
    private readonly IPublishEndpointProvider _inner = Substitute.For<IPublishEndpointProvider>();
    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly OutboxDispatchContext _dispatchContext = new();

    private OutboxPublishEndpointProvider BuildProvider() =>
        new(_inner, _store, _dispatchContext);

    [Fact]
    public async Task GetPublishSendEndpoint_ReturnsEndpointThatAddsToStore()
    {
        _inner.GetPublishSendEndpoint<TestMessage>()
            .Returns(Substitute.For<ISendEndpoint>());

        var provider = BuildProvider();
        var endpoint = await provider.GetPublishSendEndpoint<TestMessage>();

        await endpoint.Send(new TestMessage());

        _store.Received(1).Add(Arg.Any<OutboxMessage>());
    }

    [Fact]
    public async Task GetPublishSendEndpoint_SetsIsPublishTrue_InBuiltMessage()
    {
        _inner.GetPublishSendEndpoint<TestMessage>()
            .Returns(Substitute.For<ISendEndpoint>());

        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        var endpoint = await BuildProvider().GetPublishSendEndpoint<TestMessage>();
        await endpoint.Send(new TestMessage());

        captured!.DispatcherContext[Constants.ContextKeys.IsPublish].Should().Be("true");
    }

    [Fact]
    public void ConnectPublishObserver_DelegatesToInner()
    {
        var observer = Substitute.For<IPublishObserver>();

        BuildProvider().ConnectPublishObserver(observer);

        _inner.Received(1).ConnectPublishObserver(observer);
    }
}
