using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using HybridOutbox.MassTransit.Tests.Helpers;
using MassTransit;
using Newtonsoft.Json;
using NSubstitute;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxSendEndpointTests
{
    private readonly ISendEndpoint _inner = Substitute.For<ISendEndpoint>();
    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly OutboxDispatchContext _dispatchContext = new();
    private readonly Uri _address = new("rabbitmq://localhost/test-queue");

    private OutboxSendEndpoint BuildEndpoint(bool isPublish = false, Uri? address = null) =>
        new(_inner, _store, _dispatchContext, address ?? _address, isPublish);

    private OutboxMessage? CaptureAdded()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));
        return null;
    }

    [Fact]
    public async Task Send_AddsToStore_WhenNotDispatching()
    {
        var endpoint = BuildEndpoint();

        await endpoint.Send(new TestMessage { Value = "hello" });

        _store.Received(1).Add(Arg.Any<OutboxMessage>());
    }

    [Fact]
    public async Task Send_ForwardsToInner_WhenDispatching()
    {
        _inner.Send(Arg.Any<TestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        using var _ = _dispatchContext.BeginDispatch();

        var endpoint = BuildEndpoint();
        await endpoint.Send(new TestMessage());

        _store.DidNotReceive().Add(Arg.Any<OutboxMessage>());
        await _inner.Received(1).Send(Arg.Any<TestMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_SetsDispatcherKind_ToMassTransit()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        await BuildEndpoint().Send(new TestMessage());

        captured!.DispatcherKind.Should().Be(Constants.DispatcherKind);
    }

    [Fact]
    public async Task Send_SetsIsPublishFalse_ForSendEndpoint()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        await BuildEndpoint(isPublish: false).Send(new TestMessage());

        captured!.DispatcherContext[Constants.ContextKeys.IsPublish].Should().Be("false");
    }

    [Fact]
    public async Task Send_SetsIsPublishTrue_ForPublishEndpoint()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        await BuildEndpoint(isPublish: true).Send(new TestMessage());

        captured!.DispatcherContext[Constants.ContextKeys.IsPublish].Should().Be("true");
    }

    [Fact]
    public async Task Send_SetsClrType_FromMessageType()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        await BuildEndpoint().Send(new TestMessage());

        captured!.ClrType.Should().Contain(nameof(TestMessage));
    }

    [Fact]
    public async Task Send_SetsBody_AsJsonSerialization()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        var msg = new TestMessage { Value = "test-value" };
        await BuildEndpoint().Send(msg);

        var deserialized = JsonConvert.DeserializeObject<TestMessage>(captured!.Body);
        deserialized!.Value.Should().Be("test-value");
    }

    [Fact]
    public async Task Send_SetsDestinationAddress_FromConstructor()
    {
        OutboxMessage? captured = null;
        _store.When(x => x.Add(Arg.Any<OutboxMessage>()))
            .Do(ci => captured = ci.ArgAt<OutboxMessage>(0));

        await BuildEndpoint(address: _address).Send(new TestMessage());

        captured!.DestinationAddress.Should().Be(_address.ToString());
    }

    [Fact]
    public async Task Send_Object_AddsToStore_WhenNotDispatching()
    {
        var endpoint = BuildEndpoint();

        await endpoint.Send((object)new TestMessage { Value = "via-object" }, typeof(TestMessage));

        _store.Received(1).Add(Arg.Any<OutboxMessage>());
    }

    [Fact]
    public void ConnectSendObserver_DelegatesToInner()
    {
        var observer = Substitute.For<ISendObserver>();
        var endpoint = BuildEndpoint();

        endpoint.ConnectSendObserver(observer);

        _inner.Received(1).ConnectSendObserver(observer);
    }
}
