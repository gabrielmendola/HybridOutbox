namespace HybridOutbox.DynamoDb.Configuration;

public sealed class DynamoDbOptions
{
    public DynamoDbOutboxOptions Outbox { get; }
    public DynamoDbInboxOptions Inbox { get; }
    public DynamoDbLockOptions Lock { get; }

    /// <summary>
    /// Sets the DynamoDB table name for all entity types (outbox, inbox, lock) at once.
    /// Use this for single-table mode. Defaults to "HybridOutbox".
    /// </summary>
    public string TableName
    {
        set
        {
            Outbox.TableName = value;
            Inbox.TableName = value;
            Lock.TableName = value;
        }
    }

    internal DynamoDbOptions(DynamoDbOutboxOptions outbox, DynamoDbInboxOptions inbox, DynamoDbLockOptions lockOptions)
    {
        Outbox = outbox;
        Inbox = inbox;
        Lock = lockOptions;
    }
}