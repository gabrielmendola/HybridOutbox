using FluentAssertions;
using HybridOutbox.MassTransit.Internals;
using MassTransit;
using MassTransit.Serialization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.MassTransit.Tests;

public sealed class OutboxDispatcherTests
{
    private readonly IBusControl _busControl = Substitute.For<IBusControl>();
    private readonly ILogger<OutboxDispatcher> _logger = Substitute.For<ILogger<OutboxDispatcher>>();
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _dispatcher = new OutboxDispatcher(_busControl, _logger);
    }

    private static OutboxMessage SendMessage(string destination, string body = "{}")
    {
        return new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            DispatcherContext = new Dictionary<string, string>
            {
                [OutboxConstants.ConversationId] = Guid.NewGuid().ToString()
            },
            Body = body,
            DestinationAddress = destination
        };
    }

    [Fact]
    public async Task DispatchAsync_SendsMessageViaEndpoint()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.Send(Arg.Any<SerializedMessageBody>(), Arg.Any<IPipe<SendContext<SerializedMessageBody>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _busControl.GetSendEndpoint(Arg.Any<Uri>()).Returns(sendEndpoint);

        await _dispatcher.DispatchAsync(SendMessage("rabbitmq://localhost/test-queue"));

        await sendEndpoint.Received(1).Send(
            Arg.Any<SerializedMessageBody>(),
            Arg.Any<IPipe<SendContext<SerializedMessageBody>>>(),
            Arg.Any<CancellationToken>());
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
        _busControl.GetSendEndpoint(Arg.Any<Uri>())
            .ThrowsAsync(new Exception("Bus failure"));

        var act = () => _dispatcher.DispatchAsync(SendMessage("rabbitmq://localhost/test-queue"));

        await act.Should().ThrowAsync<Exception>().WithMessage("Bus failure");
    }

    [Fact]
    public async Task DispatchAsync_PropagatesHeaders_WhenSending()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        sendEndpoint.Send(Arg.Any<SerializedMessageBody>(), Arg.Any<IPipe<SendContext<SerializedMessageBody>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _busControl.GetSendEndpoint(Arg.Any<Uri>()).Returns(sendEndpoint);

        var convId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            DispatcherContext = new Dictionary<string, string>
            {
                [OutboxConstants.ConversationId] = convId.ToString()
            },
            Body = "{}",
            DestinationAddress = "rabbitmq://localhost/test-queue",
            CorrelationId = Guid.NewGuid(),
            Headers = new Dictionary<string, object> { ["X-Custom"] = "value" }
        };

        await _dispatcher.DispatchAsync(message);

        await sendEndpoint.Received(1).Send(
            Arg.Any<SerializedMessageBody>(),
            Arg.Any<IPipe<SendContext<SerializedMessageBody>>>(),
            Arg.Any<CancellationToken>());
    }
}