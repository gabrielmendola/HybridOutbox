using Amazon.DynamoDBv2.DataModel;
using MassTransit;
using Sample.WebApi.Data;
using Sample.WebApi.Entities;
using Sample.WebApi.Messages;

namespace Sample.WebApi.Consumers;

public class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;
    private readonly UnitOfWork _unitOfWork;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger, UnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
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

        _unitOfWork.Add(auditLog);
        await _unitOfWork.CommitAsync(context.CancellationToken);

        _logger.LogInformation("Audit log written for OrderId={OrderId}", evt.OrderId);
    }
}