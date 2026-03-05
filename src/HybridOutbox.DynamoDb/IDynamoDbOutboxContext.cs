using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.Abstractions;

namespace HybridOutbox.DynamoDb;

public interface IDynamoDbOutboxContext : IOutboxContext
{
    ITransactWrite[] GetTransactWrite();
}