using MassTransit;
using Sample.WebApi.Data;
using Sample.WebApi.Entities;
using Sample.WebApi.Messages;

namespace Sample.WebApi.Consumers;

/// <summary>
/// Processes a PlaceOrder command.
///
/// This consumer demonstrates the core HybridOutbox pattern:
///   1. Stage a domain write (Order) in the UnitOfWork.
///   2. Publish a downstream event (OrderPlaced) via context.Publish — the outbox
///      middleware intercepts this and stages it alongside the domain write.
///   3. Call CommitAsync — writes the Order and the outbox message to DynamoDB
///      in a single DynamoDB transaction. If the process crashes after this point,
///      the recovery job will still dispatch the OrderPlaced event.
/// </summary>
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

        // 1. Build the domain entity to persist.
        var order = new Order
        {
            OrderId = cmd.OrderId.ToString(),
            ProductName = cmd.ProductName,
            Quantity = cmd.Quantity,
            PlacedAt = DateTime.UtcNow
        };

        // 2. Stage the domain write — not yet committed.
        _unitOfWork.Add(order);

        // 3. Publish the downstream event.
        //    The outbox middleware intercepts this call: the message is NOT sent to
        //    the broker yet. It is staged alongside the domain write so both go out
        //    in the same DynamoDB transaction.
        await context.Publish(new OrderPlaced
        {
            OrderId = cmd.OrderId,
            ProductName = cmd.ProductName,
            Quantity = cmd.Quantity,
            PlacedAt = order.PlacedAt
        });

        // 4. Atomically commit: saves Order + outbox message to DynamoDB, then
        //    signals the in-memory channel to dispatch OrderPlaced immediately.
        await _unitOfWork.CommitAsync(context.CancellationToken);

        _logger.LogInformation("Order {OrderId} saved and OrderPlaced event queued", cmd.OrderId);
    }
}
