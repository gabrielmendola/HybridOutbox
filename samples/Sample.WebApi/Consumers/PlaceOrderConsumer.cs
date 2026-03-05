using MassTransit;
using Sample.WebApi.Data;
using Sample.WebApi.Entities;
using Sample.WebApi.Messages;

namespace Sample.WebApi.Consumers;

public class PlaceOrderConsumer : IConsumer<PlaceOrder>
{
    private readonly ILogger<PlaceOrderConsumer> _logger;
    private readonly UnitOfWork _unitOfWork;

    public PlaceOrderConsumer(ILogger<PlaceOrderConsumer> logger, UnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task Consume(ConsumeContext<PlaceOrder> context)
    {
        var cmd = context.Message;

        _logger.LogInformation(
            "Processing PlaceOrder: OrderId={OrderId}, Product={Product}, Qty={Qty}",
            cmd.OrderId, cmd.ProductName, cmd.Quantity);

        var order = new Order
        {
            OrderId = cmd.OrderId.ToString(),
            ProductName = cmd.ProductName,
            Quantity = cmd.Quantity,
            PlacedAt = DateTime.UtcNow
        };

        _unitOfWork.Add(order);

        await context.Publish(new OrderPlaced
        {
            OrderId = cmd.OrderId,
            ProductName = cmd.ProductName,
            Quantity = cmd.Quantity,
            PlacedAt = order.PlacedAt
        });

        await _unitOfWork.CommitAsync(context.CancellationToken);

        _logger.LogInformation("Order {OrderId} saved and OrderPlaced event queued", cmd.OrderId);
    }
}