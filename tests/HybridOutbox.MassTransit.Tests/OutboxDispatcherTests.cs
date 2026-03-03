using FluentAssertions;
using HybridOutbox.MassTransit.Tests.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxDispatcherTests
{
    private readonly IBus _bus = Substitute.For<IBus>();
    private readonly ILogger<OutboxDispatcher> _logger = Substitute.For<ILogger<OutboxDispatcher>>();
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _dispatcher = new OutboxDispatcher(_bus, _logger);
    }

    private static OutboxMessage PublishMessage(string? clrType, string body = "{}") => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        DispatcherContext = new Dictionary<string, string> { [Constants.ContextKeys.IsPublish] = "true" },
        ClrType = clrType,
        Body = body,
        DestinationAddress = string.Empty
    };

    private static OutboxMessage SendMessage(string destination, string body = "{}") => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        DispatcherContext = new Dictionary<string, string> { [Constants.ContextKeys.IsPublish] = "false" },
        Body = body,
        DestinationAddress = destination
    };

    [Fact]
    public async Task DispatchAsync_PublishesMessage_WhenIsPublishTrue()
    {
        var clrType = typeof(TestMessage).AssemblyQualifiedName!;
        var message = PublishMessage(clrType, """{"Value": "hello"}""");

        _bus.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _dispatcher.DispatchAsync(message);

        await _bus.Received(1).Publish(
            Arg.Is<object>(o => o is TestMessage),
            typeof(TestMessage),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_SendsMessage_WhenIsPublishFalse()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.Send(Arg.Any<JObject>(), Arg.Any<IPipe<SendContext<JObject>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _bus.GetSendEndpoint(Arg.Any<Uri>()).Returns(sendEndpoint);

        await _dispatcher.DispatchAsync(SendMessage("rabbitmq://localhost/test-queue"));

        await sendEndpoint.Received(1).Send(
            Arg.Any<JObject>(),
            Arg.Any<IPipe<SendContext<JObject>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DoesNotPublish_WhenClrTypeIsNull()
    {
        await _dispatcher.DispatchAsync(PublishMessage(clrType: null));

        await _bus.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DoesNotPublish_WhenClrTypeCannotBeResolved()
    {
        await _dispatcher.DispatchAsync(PublishMessage("Invalid.Type, FakeAssembly"));

        await _bus.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenDestinationAddressIsInvalid()
    {
        var act = () => _dispatcher.DispatchAsync(SendMessage("not-a-valid-uri"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not-a-valid-uri*");
    }

    [Fact]
    public async Task DispatchAsync_LogsErrorAndRethrows_WhenExceptionOccurs()
    {
        _bus.GetSendEndpoint(Arg.Any<Uri>())
            .ThrowsAsync(new Exception("Bus failure"));

        var act = () => _dispatcher.DispatchAsync(SendMessage("rabbitmq://localhost/test-queue"));

        await act.Should().ThrowAsync<Exception>().WithMessage("Bus failure");
    }

    [Fact]
    public async Task DispatchAsync_PropagatesHeaders_WhenSending()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.Send(Arg.Any<JObject>(), Arg.Any<IPipe<SendContext<JObject>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _bus.GetSendEndpoint(Arg.Any<Uri>()).Returns(sendEndpoint);

        var message = SendMessage("rabbitmq://localhost/test-queue");
        message.CorrelationId = Guid.NewGuid().ToString();
        message.ConversationId = Guid.NewGuid().ToString();
        message.Headers["X-Custom"] = "value";

        await _dispatcher.DispatchAsync(message);

        await sendEndpoint.Received(1).Send(
            Arg.Any<JObject>(),
            Arg.Any<IPipe<SendContext<JObject>>>(),
            Arg.Any<CancellationToken>());
    }
}
