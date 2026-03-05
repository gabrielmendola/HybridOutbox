using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Sample.WebApi.Entities;

namespace Sample.WebApi.Data;

public class AuditLogRepository
{
    private readonly IDynamoDBContext _context;

    public AuditLogRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task<List<AuditLog>> GetAllAsync(CancellationToken ct = default)
    {
        var search = _context.ScanAsync<AuditLog>(new List<ScanCondition>());
        return await search.GetRemainingAsync(ct);
    }
}