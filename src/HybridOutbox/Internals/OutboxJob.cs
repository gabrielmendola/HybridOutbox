using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.Internals;

internal sealed class OutboxJob : BackgroundService
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxDispatcher _dispatcher;
    private readonly IOutboxJobLock _jobLock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxJob> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString();

    public OutboxJob(
        IOutboxRepository repository,
        IOutboxDispatcher dispatcher,
        IOutboxJobLock jobLock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxJob> logger)
    {
        _repository = repository;
        _dispatcher = dispatcher;
        _jobLock = jobLock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Job.Enabled)
        {
            _logger.LogInformation("OutboxRecoveryJob is disabled.");
            return;
        }

        _logger.LogInformation(
            "OutboxRecoveryJob started. Interval={Interval}, Threshold={Threshold}",
            _options.Job.Interval, _options.Processing.Threshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Job.Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunTickAsync(stoppingToken);
        }

        _logger.LogInformation("OutboxRecoveryJob stopped.");
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        var acquired = await _jobLock.TryAcquireAsync(
            _instanceId, _options.Job.Lock.Duration, ct);

        if (!acquired)
        {
            _logger.LogDebug("OutboxRecoveryJob: job lock not acquired, skipping tick");
            return;
        }

        await RunRecoveryAsync(ct);
    }

    private async Task RunRecoveryAsync(CancellationToken ct)
    {
        IReadOnlyList<OutboxMessage> messages;

        try
        {
            messages = await _repository.GetUnprocessedAsync(
                _options.Processing.Threshold, _options.Processing.BatchSize, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OutboxRecoveryJob: failed to query unprocessed messages");
            return;
        }

        if (messages.Count == 0) return;

        _logger.LogDebug("OutboxRecoveryJob: found {Count} stale message(s) to recover", messages.Count);

        foreach (var message in messages)
        {
            if (ct.IsCancellationRequested) break;
            await RecoverMessageAsync(message, ct);
        }
    }

    private async Task RecoverMessageAsync(OutboxMessage message, CancellationToken ct)
    {
        bool locked;

        try
        {
            locked = await _repository.TryAcquireLockAsync(
                message.MessageId, _options.Processing.Lock.Duration, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "OutboxRecoveryJob: failed to acquire lock for message {MessageId}", message.MessageId);
            return;
        }

        if (!locked)
        {
            _logger.LogDebug(
                "OutboxRecoveryJob: message {MessageId} is already locked by another instance, skipping",
                message.MessageId);
            return;
        }

        try
        {
            await _dispatcher.DispatchAsync(message, ct);

            await _repository.MarkAsProcessedAsync(message.MessageId, ct);

            _logger.LogDebug("OutboxRecoveryJob: message {MessageId} recovered and dispatched", message.MessageId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "OutboxRecoveryJob: failed to dispatch message {MessageId}, releasing lock", message.MessageId);

            try
            {
                await _repository.ReleaseLockAsync(message.MessageId, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                _logger.LogError(releaseEx,
                    "OutboxRecoveryJob: failed to release lock for message {MessageId}", message.MessageId);
            }
        }
    }
}