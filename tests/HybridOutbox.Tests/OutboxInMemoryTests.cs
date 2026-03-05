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
    private readonly FakeLogger<OutboxInMemory> _logger;

    public OutboxInMemoryTests()
    {
        _dispatcher = Substitute.For<IOutboxDispatcher>();
        _repository = Substitute.For<IOutboxRepository>();
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
            Options.Create(options ?? new OutboxOptions
                { InMemory = new OutboxOptions.InMemoryOptions { DispatchConcurrency = 1 } }),
            provider,
            _logger);
    }

    [Fact]
    public async Task WhenMessageWrittenToChannel_DispatchesItAndMarksAsProcessed()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var msgId = new Guid("00000000-0000-0000-0000-000000000001");
        var message = new OutboxMessage { MessageId = msgId, DestinationAddress = "queue://test" };
        channel.Writer.TryWrite(message);

        var processed = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync(msgId, Arg.Any<CancellationToken>()))
            .Do(_ => processed.TrySetResult());

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        await _dispatcher.Received(1).DispatchAsync(message, Arg.Any<CancellationToken>());
        await _repository.Received(1).MarkAsProcessedAsync(msgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDispatchFails_LogsError_AndDoesNotMarkAsProcessed()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var msgId = new Guid("00000000-0000-0000-0000-000000000002");
        var message = new OutboxMessage { MessageId = msgId, DestinationAddress = "queue://test" };
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

        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _logger.HasError(msgId.ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task WhenMarkAsProcessedFails_LogsWarning()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var msgId = new Guid("00000000-0000-0000-0000-000000000003");
        var message = new OutboxMessage { MessageId = msgId };
        channel.Writer.TryWrite(message);

        _repository
            .MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("storage error"));

        var warningLogged = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(_ => warningLogged.TrySetResult());

        using var cts = new CancellationTokenSource();
        var consumer = CreateConsumer(channel.Reader);
        await consumer.StartAsync(cts.Token);

        await warningLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await consumer.StopAsync(CancellationToken.None);

        _logger.HasWarning(msgId.ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleMessages_AreAllDispatched()
    {
        var channel = Channel.CreateUnbounded<OutboxMessage>();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
            channel.Writer.TryWrite(new OutboxMessage { MessageId = id });

        var dispatched = new List<Guid>();
        var allDone = new TaskCompletionSource();

        _repository
            .When(r => r.MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                lock (dispatched)
                {
                    dispatched.Add(call.Arg<Guid>());
                }

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