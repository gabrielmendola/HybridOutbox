using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using HybridOutbox;
using HybridOutbox.DynamoDb;
using HybridOutbox.MassTransit;
using MassTransit;
using Sample.WebApi;
using Sample.WebApi.Consumers;
using Sample.WebApi.Data;
using Sample.WebApi.Messages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var dynamoDbConfig = new AmazonDynamoDBConfig();
var serviceUrl = builder.Configuration.GetValue<string>("DynamoDB:ServiceUrl");
if (!string.IsNullOrEmpty(serviceUrl)) dynamoDbConfig.ServiceURL = serviceUrl;

builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(dynamoDbConfig));
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContextBuilder().WithDynamoDBClient(() => client).Build();
});

builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddSingleton<AuditLogRepository>();

builder.Services
    .AddHybridOutbox()
    .AddDynamoDb(options =>
    {
        // Single-table mode: all entity types share one DynamoDB table.
        // options.TableName = "HybridOutbox"; // default — uncomment to override
        options.TableName = "outbox";
        options.Outbox.RetentionPeriod = TimeSpan.FromDays(7);
        options.Inbox.RetentionPeriod = TimeSpan.FromDays(7);
    });

builder.Services.AddMassTransit(bus =>
{
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

    bus.AddConfigureEndpointsCallback((context, name, cfg) => { cfg.UseHybridOutbox(context); });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dynamoDbClient = scope.ServiceProvider.GetRequiredService<IAmazonDynamoDB>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DynamoDbInitializer.InitializeAsync(dynamoDbClient, logger);

    var tableInitializer = scope.ServiceProvider.GetRequiredService<DynamoDbTableInitializer>();
    await tableInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();

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

app.MapGet("/audit-logs", async (AuditLogRepository repo, CancellationToken ct) =>
        Results.Ok(await repo.GetAllAsync(ct)))
    .WithName("GetAuditLogs")
    .WithSummary("List audit logs — each entry proves an OrderPlaced event was dispatched by the outbox");

app.Run();

namespace Sample.WebApi
{
    internal record PlaceOrderRequest(string ProductName, int Quantity);
}