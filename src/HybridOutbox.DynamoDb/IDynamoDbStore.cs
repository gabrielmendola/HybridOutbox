using Amazon.DynamoDBv2.DataModel;
using HybridOutbox.Abstractions;

namespace HybridOutbox.DynamoDb;

public interface IDynamoDbStore : IOutboxStore
{
    ITransactWrite<DynamoDbOutboxMessage> GetTransactWrite();
}