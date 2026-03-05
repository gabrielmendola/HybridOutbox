using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Sample.WebApi.Entities;

namespace Sample.WebApi.Data;

public class OrderRepository
{
    private readonly IDynamoDBContext _context;

    public OrderRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(string orderId, CancellationToken ct = default)
    {
        return await _context.LoadAsync<Order>(orderId, ct);
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        var search = _context.ScanAsync<Order>(new List<ScanCondition>());
        return await search.GetRemainingAsync(ct);
    }
}