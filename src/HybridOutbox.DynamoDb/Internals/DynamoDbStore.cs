using System.Threading.Channels;
using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb.Internals;

internal class DynamoDbStore : OutboxStore, IDynamoDbStore
{
    private readonly IDynamoDBContext _context;
    private readonly DynamoDbOutboxOptions _dbOptions;
    private readonly Guid _id;

    private TransactWriteConfig TransactCfg => new() { OverrideTableName = _dbOptions.TableName };

    public DynamoDbStore(
        IDynamoDBContext context,
        IOptions<DynamoDbOutboxOptions> dbOptions,
        ChannelWriter<OutboxMessage> channel,
        ILogger<OutboxStore> logger)
        : base(channel, logger)
    {
        _id = Guid.NewGuid();
        _context = context;
        _dbOptions = dbOptions.Value;
    }

    public ITransactWrite<DynamoDbOutboxMessage> GetTransactWrite()
    {
        var expiresAt = _dbOptions.RetentionPeriod.HasValue
            ? DateTimeOffset.UtcNow.Add(_dbOptions.RetentionPeriod.Value).ToUnixTimeSeconds()
            : (long?)null;

        var items = GetUndispatchedMessages().Select(m =>
        {
            var record = DynamoDbOutboxMessage.FromMessage(m);
            record.ExpiresAt = expiresAt;
            return record;
        });

        var write = _context.CreateTransactWrite<DynamoDbOutboxMessage>(TransactCfg);
        write.AddSaveItems(items);
        return write;
    }
}
