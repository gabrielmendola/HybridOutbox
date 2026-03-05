using System.Diagnostics;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using Microsoft.Extensions.Options;

namespace HybridOutbox.MassTransit.Internals;

public class OutboxContextFactory : IOutboxContextFactory
{
    private readonly IOutboxContext _outboxContext;
    private readonly IServiceProvider _provider;
    private readonly IInboxRepository _inboxRepository;
    private readonly IOptions<OutboxOptions> _options;

    public OutboxContextFactory(
        IOutboxContext outboxContext,
        IServiceProvider provider,
        IInboxRepository inboxRepository,
        IOptions<OutboxOptions> options)
    {
        _outboxContext = outboxContext;
        _provider = provider;
        _inboxRepository = inboxRepository;
        _options = options;
    }

    public async Task Send<T>(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next, string consumerType)
        where T : class
    {
        if (_options.Value.Inbox.Enabled && context.MessageId is not null)
        {
            var messageId = context.MessageId.Value;

            if (await _inboxRepository.ExistsAsync(messageId, consumerType, context.CancellationToken)
                    .ConfigureAwait(false))
                return;

            _outboxContext.Add(new InboxMessage { MessageId = messageId, ConsumerType = consumerType });
        }

        var timer = Stopwatch.StartNew();
        var outboxContext = new OutboxConsumeContext<T>(context, _provider, _outboxContext);

        try
        {
            await next.Send(outboxContext).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await context.NotifyFaulted(timer.Elapsed, TypeCache<T>.ShortName, exception).ConfigureAwait(false);

            throw;
        }
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("outbox");
    }
}