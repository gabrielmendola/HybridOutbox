using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using HybridOutbox;
using HybridOutbox.DynamoDb;
using HybridOutbox.MassTransit;
using MassTransit;
using Sample.WebApi.Consumers;
using Sample.WebApi.Data;
using Sample.WebApi.Messages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// ── AWS / DynamoDB ────────────────────────────────────────────────────────────

var dynamoDbConfig = new AmazonDynamoDBConfig();
var serviceUrl = builder.Configuration.GetValue<string>("DynamoDB:ServiceUrl");
if (!string.IsNullOrEmpty(serviceUrl)) dynamoDbConfig.ServiceURL = serviceUrl;

builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(dynamoDbConfig));
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContextBuilder().WithDynamoDBClient(() => client).Build();
});

// ── Application services ──────────────────────────────────────────────────────

builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddSingleton<AuditLogRepository>();

// ── HybridOutbox ──────────────────────────────────────────────────────────────
//
// AddHybridOutbox registers:
//   - Background recovery job (polls DynamoDB for unprocessed outbox messages)
//   - In-memory channel (fast-path dispatch right after CommitAsync)
//   - NoOpJobLock (replaced by DynamoDbJobLock when AddDynamoDb is called)
//
// AddDynamoDb registers:
//   - IOutboxRepository  → DynamoDbOutboxRepository
//   - IOutboxJobLock     → DynamoDbJobLock (distributed lock via DynamoDB)
//   - IDynamoDbStore     → used by UnitOfWork to write outbox messages in a
//                          single DynamoDB transaction with the domain data
builder.Services
    .AddHybridOutbox()
    .AddDynamoDb(options =>
    {
        options.TableName = "HybridOutbox";
        options.RetentionPeriod = TimeSpan.FromDays(7);
    });

// ── MassTransit / RabbitMQ ────────────────────────────────────────────────────

builder.Services.AddMassTransit(bus =>
{
    // Registers outbox middleware that intercepts Publish/Send in ALL scopes —
    // both HTTP handlers and consumer contexts. Messages are staged in the
    // UnitOfWork and only dispatched after CommitAsync is called.
    bus.AddHybridOutbox();

    bus.AddConsumer<PlaceOrderConsumer>();
    bus.AddConsumer<OrderPlacedConsumer>();

    bus.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var username = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
        var password = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(username);
            h.Password(password);
        });
        
        cfg.ConfigureEndpoints(context);
    });
    
    bus.AddConfigureEndpointsCallback((context, name, cfg) =>
    {
        cfg.UseHybridOutbox(context);
    });
});

var app = builder.Build();

// ── Table initialization ──────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var dynamoDbClient = scope.ServiceProvider.GetRequiredService<IAmazonDynamoDB>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Creates Orders and AuditLogs tables if they don't exist.
    await DynamoDbInitializer.InitializeAsync(dynamoDbClient, logger);

    // Creates the HybridOutbox table (with GSI) and lock table if configured.
    var outboxInitializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
    await outboxInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Place a new order.
//
// Flow:
//   POST /orders
//     → publishEndpoint.Publish(PlaceOrder) — outbox intercepts this too;
//       the message is staged, not sent to the broker yet
//     → CommitAsync() — PlaceOrder written to the outbox table in DynamoDB
//       → in-memory channel dispatches PlaceOrder to RabbitMQ
//         → PlaceOrderConsumer:
//             1. stages Order write in UnitOfWork
//             2. publishes OrderPlaced — outbox intercepts again
//             3. CommitAsync → Order + OrderPlaced outbox message written atomically
//                → in-memory channel dispatches OrderPlaced to RabbitMQ
//                  → OrderPlacedConsumer writes AuditLog
//           (if pod crashes after step 3, recovery job dispatches OrderPlaced)
app.MapPost("/orders", async (
        PlaceOrderRequest request,
        IPublishEndpoint publishEndpoint,
        UnitOfWork unitOfWork,
        CancellationToken cancellationToken) =>
    {
        var orderId = Guid.NewGuid();

        await publishEndpoint.Publish(
            new PlaceOrder { OrderId = orderId, ProductName = request.ProductName, Quantity = request.Quantity },
            cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return Results.Accepted($"/orders/{orderId}", new { OrderId = orderId });
    })
    .WithName("PlaceOrder")
    .WithSummary("Place a new order (triggers the outbox flow)");

// Query orders saved by PlaceOrderConsumer.
app.MapGet("/orders", async (OrderRepository repo, CancellationToken ct) =>
        Results.Ok(await repo.GetAllAsync(ct)))
    .WithName("GetOrders")
    .WithSummary("List all saved orders");

app.MapGet("/orders/{id}", async (string id, OrderRepository repo, CancellationToken ct) =>
    {
        var order = await repo.GetByIdAsync(id, ct);
        return order is null ? Results.NotFound() : Results.Ok(order);
    })
    .WithName("GetOrder")
    .WithSummary("Get a single order by ID");

// Query audit logs written by OrderPlacedConsumer (proof the outbox dispatched the event).
app.MapGet("/audit-logs", async (AuditLogRepository repo, CancellationToken ct) =>
        Results.Ok(await repo.GetAllAsync(ct)))
    .WithName("GetAuditLogs")
    .WithSummary("List audit logs — each entry proves an OrderPlaced event was dispatched by the outbox");

app.Run();

// ── Request models ─────────────────────────────────────────────────────────────

record PlaceOrderRequest(string ProductName, int Quantity);
