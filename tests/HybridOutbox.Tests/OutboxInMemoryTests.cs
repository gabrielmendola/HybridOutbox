using System.Threading.Channels;
using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.Internals;
using HybridOutbox.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxInMemoryTests
{
    private readonly IOutboxDispatcher _dispatcher;
    private readonly IOutboxRepository _repository;
    private readonly OutboxDispatchContext _dispatchContext;
    private readonly FakeLogger<OutboxInMemory> _logger;

    public OutboxInMemoryTests()
    {
        _dispatcher = Substitute.For<IOutboxDispatcher>();
        _repository = Substitute.For<IOutboxRepository>();
        _dispatchContext = new OutboxDispatchContext();
        _logger = new FakeLogger<OutboxInMemory>();
    }

    private OutboxInMemory CreateConsumer(
        ChannelReader<OutboxMessage> channelReader,
        OutboxOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_dispatcher);
        services.AddSingleton(_repository);
        var provider = services.BuildServiceProvider();

        return new OutboxInMemory(
            channelReader,
            Options.Create(options ?? new OutboxOptions { InMemory = new OutboxOptions.InMemoryOptions { DispatchConcurrency = 1 } }),
            provider,
            _dispatchContext,
            _logger);
    }

    [Fact]
    public async Task WhenMessageWrittenToChannel_DispatchesItAndMarksAsProcessed()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var message = new OutboxMessage { MessageId = "msg-1", DestinationAddress = "queue://test" };
        channel.Writer.TryWrite(message);

        var processed = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync("msg-1", Arg.Any<CancellationToken>()))
            .Do(_ => processed.TrySetResult());

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        await _dispatcher.Received(1).DispatchAsync(message, Arg.Any<CancellationToken>());
        await _repository.Received(1).MarkAsProcessedAsync("msg-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDispatchFails_LogsError_AndDoesNotMarkAsProcessed()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var message = new OutboxMessage { MessageId = "msg-fail", DestinationAddress = "queue://test" };
        channel.Writer.TryWrite(message);

        _dispatcher
            .DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var errorLogged = new TaskCompletionSource();
        _dispatcher
            .When(_ => _.DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()))
            .Do(_ => errorLogged.TrySetResult());

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _logger.HasError("msg-fail").Should().BeTrue();
    }

    [Fact]
    public async Task WhenMarkAsProcessedFails_LogsWarning()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var message = new OutboxMessage { MessageId = "msg-mark-fail" };
        channel.Writer.TryWrite(message);

        _repository
            .MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("storage error"));

        var warningLogged = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => warningLogged.TrySetResult());

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await warningLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        _logger.HasWarning("msg-mark-fail").Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_SetsIsOutboxDispatchingToTrue_DuringDispatch()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var message = new OutboxMessage { MessageId = "msg-ctx" };
        channel.Writer.TryWrite(message);

        bool? isDispatchingDuringCall = null;
        var dispatched = new TaskCompletionSource();

        _dispatcher
            .When(d => d.DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                isDispatchingDuringCall = _dispatchContext.IsOutboxDispatching;
                dispatched.TrySetResult();
            });

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        isDispatchingDuringCall.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleMessages_AreAllDispatched()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var ids = new[] { "m1", "m2", "m3" };
        foreach (var id in ids)
            channel.Writer.TryWrite(new OutboxMessage { MessageId = id });

        var dispatched = new List<string>();
        var allDone = new TaskCompletionSource();

        _repository
            .When(r => r.MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                lock (dispatched) dispatched.Add(call.Arg<string>());
                if (dispatched.Count == 3) allDone.TrySetResult();
            });

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        dispatched.Should().BeEquivalentTo(ids);
    }
}
