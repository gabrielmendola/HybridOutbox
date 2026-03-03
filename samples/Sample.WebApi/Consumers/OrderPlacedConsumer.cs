using Amazon.DynamoDBv2.DataModel;
using MassTransit;
using Sample.WebApi.Entities;
using Sample.WebApi.Messages;

namespace Sample.WebApi.Consumers;

/// <summary>
/// Handles the OrderPlaced event that was dispatched by the outbox.
///
/// This consumer demonstrates that the downstream event was successfully
/// delivered (either by the in-memory fast path or the recovery job).
/// It writes an audit log entry to DynamoDB as proof of receipt.
/// </summary>
public class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;
    private readonly IDynamoDBContext _dynamoDbContext;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger, IDynamoDBContext dynamoDbContext)
    {
        _logger = logger;
        _dynamoDbContext = dynamoDbContext;
    }

    public async Task Consume(ConsumeContext<OrderPlaced> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "OrderPlaced received (dispatched by outbox): OrderId={OrderId}",
            evt.OrderId);

        var auditLog = new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            Event = nameof(OrderPlaced),
            OrderId = evt.OrderId.ToString(),
            RecordedAt = DateTime.UtcNow
        };

        await _dynamoDbContext.SaveAsync(auditLog, context.CancellationToken);

        _logger.LogInformation("Audit log written for OrderId={OrderId}", evt.OrderId);
    }
}
