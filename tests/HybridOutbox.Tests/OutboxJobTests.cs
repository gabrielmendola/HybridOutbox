using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.Internals;
using HybridOutbox.Tests.Helpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxJobTests
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxDispatcher _dispatcher;
    private readonly IOutboxJobLock _jobLock;
    private readonly OutboxDispatchContext _dispatchContext;
    private readonly FakeLogger<OutboxJob> _logger;

    public OutboxJobTests()
    {
        _repository = Substitute.For<IOutboxRepository>();
        _dispatcher = Substitute.For<IOutboxDispatcher>();
        _jobLock = Substitute.For<IOutboxJobLock>();
        _jobLock
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _dispatchContext = new OutboxDispatchContext();
        _logger = new FakeLogger<OutboxJob>();
    }

    private OutboxJob CreateService(OutboxOptions? options = null)
    {
        options ??= new OutboxOptions
        {
            Job = new OutboxOptions.JobOptions { Interval = TimeSpan.FromMilliseconds(10) },
            Processing = new OutboxOptions.ProcessingOptions
            {
                Threshold = TimeSpan.FromSeconds(10),
                BatchSize = 100,
                Lock = new OutboxOptions.LockOptions { Duration = TimeSpan.FromSeconds(30) }
            }
        };

        return new OutboxJob(
            _repository,
            _dispatcher,
            _jobLock,
            _dispatchContext,
            Options.Create(options),
            _logger);
    }

    private static OutboxMessage MakeMessage(string id = "msg-1") =>
        new() { MessageId = id, DestinationAddress = "queue://test" };

    [Fact]
    public async Task WhenNoUnprocessedMessages_NothingIsDispatched()
    {
        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([]));

        var polled = new TaskCompletionSource();
        _repository
            .When(r => r.GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>()))
            .Do(_ => polled.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await polled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenUnprocessedMessageFound_AcquiresLock_DispatchesAndMarksProcessed()
    {
        var message = MakeMessage("msg-recovery");

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync("msg-recovery", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var processed = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync("msg-recovery", Arg.Any<CancellationToken>()))
            .Do(_ => processed.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.Received().DispatchAsync(message, Arg.Any<CancellationToken>());
        await _repository.Received().MarkAsProcessedAsync("msg-recovery", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenLockNotAcquired_MessageIsSkipped()
    {
        var message = MakeMessage("msg-locked");

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync("msg-locked", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var polled = new TaskCompletionSource();
        _repository
            .When(r => r.TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()))
            .Do(_ => polled.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await polled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDispatchFails_ReleasesLock()
    {
        var message = MakeMessage("msg-fail");

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync("msg-fail", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _dispatcher
            .DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("transport error"));

        var lockReleased = new TaskCompletionSource();
        _repository
            .When(r => r.ReleaseLockAsync("msg-fail", Arg.Any<CancellationToken>()))
            .Do(_ => lockReleased.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await lockReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _repository.Received().ReleaseLockAsync("msg-fail", CancellationToken.None);
        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGetUnprocessedThrows_LogsError_AndContinues()
    {
        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("db unavailable"));

        var errorLogged = new TaskCompletionSource();
        _repository
            .When(r => r.GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>()))
            .Do(_ => errorLogged.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        _logger.HasError("unprocessed messages").Should().BeTrue();
    }

    [Fact]
    public async Task WhenTryAcquireLockThrows_LogsError_AndSkipsMessage()
    {
        var message = MakeMessage("msg-lock-fail");

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync("msg-lock-fail", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("lock service down"));

        var lockAttempted = new TaskCompletionSource();
        _repository
            .When(r => r.TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()))
            .Do(_ => lockAttempted.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await lockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        _logger.HasError("msg-lock-fail").Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_SetsIsOutboxDispatchingToTrue_DuringRecovery()
    {
        var message = MakeMessage("msg-ctx");
        bool? isDispatchingDuringCall = null;

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var dispatched = new TaskCompletionSource();
        _dispatcher
            .When(d => d.DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                isDispatchingDuringCall = _dispatchContext.IsOutboxDispatching;
                dispatched.TrySetResult();
            });

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        isDispatchingDuringCall.Should().BeTrue();
    }
}
