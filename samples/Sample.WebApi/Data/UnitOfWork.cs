using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.DynamoDb;

namespace Sample.WebApi.Data;

public sealed class UnitOfWork : IDisposable
{
    private readonly IDynamoDBContext _context;
    private readonly IDynamoDbStore _dynamoDbStore;
    private IMultiTableTransactWrite _transaction;

    public UnitOfWork(
        IDynamoDBContext context,
        IDynamoDbStore dynamoDbStore)
    {
        _context = context;
        _dynamoDbStore = dynamoDbStore;

        _transaction = new MultiTableTransactWrite();
    }

    public void Add<T>(T entity) where T : class
    {
        var write = _context.CreateTransactWrite<T>();
        write.AddSaveItem(entity);
        _transaction.AddTransactionPart(write);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNoTransaction();

        _transaction.AddTransactionPart(_dynamoDbStore.GetTransactWrite());

        await _transaction.ExecuteAsync(cancellationToken);
        _dynamoDbStore.DispatchMessages();
    }

    public void Rollback()
    {
        ThrowIfNoTransaction();

        _transaction = new MultiTableTransactWrite();
        _dynamoDbStore.Clear();
    }

    private void ThrowIfNoTransaction()
    {
        if (_transaction is null) throw new InvalidOperationException($"No active transaction.");
    }

    public void Dispose()
    {
        _dynamoDbStore.Dispose();
    }
}