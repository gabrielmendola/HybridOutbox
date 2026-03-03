using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Sample.WebApi.Entities;

namespace Sample.WebApi.Data;

public class TestMessageRepository : ITestMessageRepository
{
    private readonly IDynamoDBContext _context;

    public TestMessageRepository(IDynamoDBContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(TestMessage message, CancellationToken cancellationToken = default)
    {
        await _context.SaveAsync(message, cancellationToken);
    }

    public async Task<TestMessage?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.LoadAsync<TestMessage>(id, cancellationToken);
    }

    public async Task<List<TestMessage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var search = _context.ScanAsync<TestMessage>(new List<ScanCondition>());
        return await search.GetRemainingAsync(cancellationToken);
    }
}