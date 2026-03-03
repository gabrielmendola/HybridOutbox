using System.Threading.Channels;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.Internals;

internal sealed class OutboxInMemory : BackgroundService
{
    private readonly ChannelReader<OutboxMessage> _channel;
    private readonly IOutboxDispatcher _dispatcher;
    private readonly IOutboxRepository _repository;
    private readonly OutboxDispatchContext _dispatchContext;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxInMemory> _logger;

    public OutboxInMemory(
        ChannelReader<OutboxMessage> channel,
        IOptions<OutboxOptions> options,
        IServiceProvider serviceProvider,
        OutboxDispatchContext dispatchContext,
        ILogger<OutboxInMemory> logger)
    {
        var scopedServiceProvider = serviceProvider.CreateScope().ServiceProvider;

        _channel = channel;
        _dispatcher = scopedServiceProvider.GetRequiredService<IOutboxDispatcher>();
        _repository = scopedServiceProvider.GetRequiredService<IOutboxRepository>();
        _dispatchContext = dispatchContext;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.InMemory.Enabled)
        {
            _logger.LogInformation("OutboxChannelConsumer is disabled.");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "OutboxChannelConsumer started. Concurrency={Concurrency}",
            _options.InMemory.DispatchConcurrency);

        return Parallel.ForEachAsync(
            _channel.ReadAllAsync(stoppingToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.InMemory.DispatchConcurrency,
                CancellationToken = stoppingToken
            },
            (message, ct) => DispatchMessageAsync(message, ct));
    }

    private async ValueTask DispatchMessageAsync(OutboxMessage message, CancellationToken ct)
    {
        try
        {
            using (_dispatchContext.BeginDispatch())
            {
                await _dispatcher.DispatchAsync(message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "OutboxChannelConsumer: dispatch failed for message {MessageId} (destination={Destination}). " +
                "The recovery job will retry after the processing threshold.",
                message.MessageId, message.DestinationAddress);
            return;
        }

        try
        {
            await _repository.MarkAsProcessedAsync(message.MessageId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "OutboxChannelConsumer: message {MessageId} was dispatched but MarkAsProcessed failed. " +
                "The recovery job may re-dispatch it.",
                message.MessageId);
        }
    }
}