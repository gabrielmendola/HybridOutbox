using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Sample.WebApi.Data;

public static class DynamoDbInitializer
{
    public static async Task InitializeAsync(IAmazonDynamoDB dynamoDbClient, ILogger logger)
    {
        var existingTables = (await dynamoDbClient.ListTablesAsync()).TableNames;

        await EnsureTableAsync(dynamoDbClient, logger, existingTables,
            "Orders",
            "OrderId",
            ScalarAttributeType.S);

        await EnsureTableAsync(dynamoDbClient, logger, existingTables,
            "AuditLogs",
            "AuditId",
            ScalarAttributeType.S);
    }

    private static async Task EnsureTableAsync(
        IAmazonDynamoDB client,
        ILogger logger,
        List<string> existingTables,
        string tableName,
        string hashKeyName,
        ScalarAttributeType hashKeyType)
    {
        if (existingTables.Contains(tableName))
        {
            logger.LogInformation("DynamoDB table {TableName} already exists", tableName);
            return;
        }

        await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement { AttributeName = hashKeyName, KeyType = KeyType.HASH }],
            AttributeDefinitions =
                [new AttributeDefinition { AttributeName = hashKeyName, AttributeType = hashKeyType }],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        logger.LogInformation("DynamoDB table {TableName} created, waiting for ACTIVE...", tableName);

        TableStatus status;
        do
        {
            await Task.Delay(1000);
            var describe = await client.DescribeTableAsync(tableName);
            status = describe.Table.TableStatus;
        } while (status == TableStatus.CREATING);

        logger.LogInformation("DynamoDB table {TableName} is now active", tableName);
    }
}