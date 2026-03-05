using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.DynamoDb;

namespace Sample.WebApi.Data;

public sealed class UnitOfWork : IDisposable
{
    private readonly IDynamoDBContext _context;
    private readonly IDynamoDbOutboxContext _outboxContext;
    private List<ITransactWrite> _writes = [];

    public UnitOfWork(IDynamoDBContext context, IDynamoDbOutboxContext outboxContext)
    {
        _context = context;
        _outboxContext = outboxContext;
    }

    public void Add<T>(T entity) where T : class
    {
        var write = _context.CreateTransactWrite<T>();
        write.AddSaveItem(entity);
        _writes.Add(write);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = new MultiTableTransactWrite();

        foreach (var write in _writes)
            transaction.AddTransactionPart(write);
        foreach (var write in _outboxContext.GetTransactWrite())
            transaction.AddTransactionPart(write);

        await transaction.ExecuteAsync(cancellationToken);
        _outboxContext.DispatchMessages();
    }

    public void Rollback()
    {
        _writes = [];
        _outboxContext.Clear();
    }

    public void Dispose()
    {
        _outboxContext.Dispose();
    }
}