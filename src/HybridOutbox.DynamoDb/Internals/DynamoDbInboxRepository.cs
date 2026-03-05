using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.Abstractions;
using HybridOutbox.DynamoDb.Configuration;
using Microsoft.Extensions.Options;

namespace HybridOutbox.DynamoDb.Internals;

internal sealed class DynamoDbInboxRepository : IInboxRepository
{
    private readonly IDynamoDBContext _context;
    private readonly DynamoDbInboxOptions _options;

    private LoadConfig LoadCfg => new() { OverrideTableName = _options.TableName };

    public DynamoDbInboxRepository(IDynamoDBContext context, IOptions<DynamoDbInboxOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<bool> ExistsAsync(Guid messageId, string consumerType, CancellationToken ct = default)
    {
        var result = await _context.LoadAsync<DynamoDbInboxMessage>(messageId.ToString(),
            DynamoDbInboxMessage.BuildSortKey(consumerType), LoadCfg, ct);
        return result is not null;
    }
}