using System.Threading.Channels;
using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb.Internals;

internal sealed class DynamoDbOutboxContext : OutboxContext, IDynamoDbOutboxContext
{
    private readonly IDynamoDBContext _context;
    private readonly DynamoDbOutboxOptions _outboxOptions;
    private readonly DynamoDbInboxOptions _inboxOptions;

    private TransactWriteConfig OutboxTransactCfg => new() { OverrideTableName = _outboxOptions.TableName };
    private TransactWriteConfig InboxTransactCfg => new() { OverrideTableName = _inboxOptions.TableName };

    public DynamoDbOutboxContext(
        IDynamoDBContext context,
        IOptions<DynamoDbOutboxOptions> outboxOptions,
        IOptions<DynamoDbInboxOptions> inboxOptions,
        ChannelWriter<OutboxMessage> channel,
        ILogger<OutboxContext> logger)
        : base(channel, logger)
    {
        _context = context;
        _outboxOptions = outboxOptions.Value;
        _inboxOptions = inboxOptions.Value;
    }

    public ITransactWrite[] GetTransactWrite()
    {
        var outboxExpiresAt = _outboxOptions.RetentionPeriod.HasValue
            ? DateTimeOffset.UtcNow.Add(_outboxOptions.RetentionPeriod.Value).ToUnixTimeSeconds()
            : (long?)null;

        var outboxItems = GetUndispatchedMessages().Select(message =>
        {
            var record = DynamoDbOutboxMessage.FromMessage(message);
            record.ExpiresAt = outboxExpiresAt;
            return record;
        });

        var outboxWrite = _context.CreateTransactWrite<DynamoDbOutboxMessage>(OutboxTransactCfg);
        outboxWrite.AddSaveItems(outboxItems);

        var staged = GetStagedInboxMessage();
        if (staged is null)
            return [outboxWrite];

        var inboxExpiresAt = _inboxOptions.RetentionPeriod.HasValue
            ? DateTimeOffset.UtcNow.Add(_inboxOptions.RetentionPeriod.Value).ToUnixTimeSeconds()
            : (long?)null;

        var inboxRecord = new DynamoDbInboxMessage
        {
            PK = staged.MessageId.ToString(),
            SK = DynamoDbInboxMessage.BuildSortKey(staged.ConsumerType),
            ConsumerType = staged.ConsumerType,
            ProcessedAt = staged.ProcessedAt.ToString("O"),
            ExpiresAt = inboxExpiresAt
        };

        var inboxWrite = _context.CreateTransactWrite<DynamoDbInboxMessage>(InboxTransactCfg);
        inboxWrite.AddSaveItem(inboxRecord);

        return [outboxWrite, inboxWrite];
    }
}