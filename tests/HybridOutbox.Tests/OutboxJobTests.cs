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
    private readonly FakeLogger<OutboxJob> _logger;

    public OutboxJobTests()
    {
        _repository = Substitute.For<IOutboxRepository>();
        _dispatcher = Substitute.For<IOutboxDispatcher>();
        _jobLock = Substitute.For<IOutboxJobLock>();
        _jobLock
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
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
            Options.Create(options),
            _logger);
    }

    private static readonly Guid IdRecovery = new("10000000-0000-0000-0000-000000000000");
    private static readonly Guid IdLocked = new("20000000-0000-0000-0000-000000000000");
    private static readonly Guid IdFail = new("30000000-0000-0000-0000-000000000000");
    private static readonly Guid IdLockFail = new("40000000-0000-0000-0000-000000000000");
    private static readonly Guid IdCtx = new("50000000-0000-0000-0000-000000000000");

    private static OutboxMessage MakeMessage(Guid id)
    {
        return new OutboxMessage { MessageId = id, DestinationAddress = "queue://test" };
    }

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
        var message = MakeMessage(IdRecovery);

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync(IdRecovery, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var processed = new TaskCompletionSource();
        _repository
            .When(r => r.MarkAsProcessedAsync(IdRecovery, Arg.Any<CancellationToken>()))
            .Do(_ => processed.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.Received().DispatchAsync(message, Arg.Any<CancellationToken>());
        await _repository.Received().MarkAsProcessedAsync(IdRecovery, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenLockNotAcquired_MessageIsSkipped()
    {
        var message = MakeMessage(IdLocked);

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync(IdLocked, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var polled = new TaskCompletionSource();
        _repository
            .When(r => r.TryAcquireLockAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()))
            .Do(_ => polled.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await polled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDispatchFails_ReleasesLock()
    {
        var message = MakeMessage(IdFail);

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync(IdFail, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _dispatcher
            .DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("transport error"));

        var lockReleased = new TaskCompletionSource();
        _repository
            .When(r => r.ReleaseLockAsync(IdFail, Arg.Any<CancellationToken>()))
            .Do(_ => lockReleased.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await lockReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _repository.Received().ReleaseLockAsync(IdFail, CancellationToken.None);
        await _repository.DidNotReceive().MarkAsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
        var message = MakeMessage(IdLockFail);

        _repository
            .GetUnprocessedAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([message]));

        _repository
            .TryAcquireLockAsync(IdLockFail, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("lock service down"));

        var lockAttempted = new TaskCompletionSource();
        _repository
            .When(r => r.TryAcquireLockAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()))
            .Do(_ => lockAttempted.TrySetResult());

        using var cts = new CancellationTokenSource();
        var service = CreateService();
        await service.StartAsync(cts.Token);

        await lockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        _logger.HasError(IdLockFail.ToString()).Should().BeTrue();
    }

}