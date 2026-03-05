# HybridOutbox

A transactional outbox implementation for .NET with dual-path message dispatch: immediate in-memory delivery for low latency plus a background recovery job for guaranteed at-least-once delivery.

[![CI](https://github.com/gabrielmendola/hybridoutbox/actions/workflows/ci.yml/badge.svg)](https://github.com/gabrielmendola/hybridoutbox/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## The Problem

When a service publishes a message inside a database transaction, two failure modes exist:

- The transaction commits but the message is never published (process crash).
- The message is published but the transaction rolls back (inconsistency).

The transactional outbox pattern solves this by writing messages to the same database as your domain data, inside the same transaction, and dispatching them afterwards.

## How It Works

HybridOutbox uses two complementary paths:

```
┌─────────────────────────────────────────────────────────────────┐
│  Consumer / Handler                                              │
│                                                                  │
│  1. Write domain data ──┐                                        │
│  2. Write outbox msg  ──┤─── same DB transaction ───► DynamoDB  │
│                          └──────────────────────────────────────┘
│                                                                  │
│  3a. In-memory channel ────────────────────────────► Dispatcher  │
│       (fast path, best-effort)                                   │
│                                                                  │
│  3b. Recovery job (periodic) ──► unprocessed msgs ► Dispatcher  │
│       (slow path, guaranteed)                                    │
└─────────────────────────────────────────────────────────────────┘
```

After a successful transaction commit, messages are queued in a `Channel<T>` for immediate delivery. If the process crashes before delivery, the periodic recovery job will pick up unprocessed messages and dispatch them after a configurable threshold.

## Packages

The core library defines abstractions that are independent of any specific database or message broker. Persistence and transport are plugged in via separate provider packages.

| Package | Description |
|---|---|
| `HybridOutbox` | Core abstractions, background services, and channel |
| `HybridOutbox.DynamoDb` | **Persistence provider** — DynamoDB (outbox table + distributed job lock) |
| `HybridOutbox.MassTransit` | **Transport provider** — MassTransit consumer middleware |

### Extensibility

Any persistence store or message transport can be supported by implementing the core interfaces:

| Interface | Role |
|---|---|
| `IOutboxRepository` | Query unprocessed messages, acquire/release per-message locks, mark as processed |
| `IOutboxStore` | Scoped in-memory staging area; receives intercepted messages before commit |
| `IOutboxDispatcher` | Dispatches a single `OutboxMessage` to the broker |
| `IOutboxJobLock` | Distributed lock to ensure only one pod runs the recovery job at a time |

> Currently only **DynamoDB** (persistence) and **MassTransit** (transport) providers are available. Contributions for other stores (PostgreSQL, MongoDB, etc.) and transports (NServiceBus, Azure Service Bus SDK, etc.) are welcome.

## Quick Start

### 1. Install packages

```bash
dotnet add package HybridOutbox
dotnet add package HybridOutbox.DynamoDb
dotnet add package HybridOutbox.MassTransit
```

### 2. Register services

```csharp
// Program.cs

// Register the AWS DynamoDB client and document context.
builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
    new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
        .Build());

builder.Services
    .AddHybridOutbox()
    .AddDynamoDb(options =>
    {
        options.TableName = "MyOutbox";
        options.RetentionPeriod = TimeSpan.FromDays(7); // TTL on processed messages
    });

builder.Services.AddMassTransit(x =>
{
    x.AddHybridOutbox(); // intercepts Publish/Send in HTTP handler scopes
    x.AddConsumer<MyConsumer>();

    // Apply the outbox consume filter to every consumer endpoint.
    // Without this, consumer-side context.Publish bypasses the outbox.
    x.AddConfigureEndpointsCallback((ctx, _, cfg) => cfg.UseHybridOutbox(ctx));

    x.UsingRabbitMq((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
});
```

### 3. Initialize the DynamoDB table

Call this once at startup before the application starts accepting requests:

```csharp
var initializer = app.Services.GetRequiredService<DynamoDbTableInitializer>();
await initializer.InitializeAsync();
```

### 4. Use the outbox in a consumer

Inject `UnitOfWork` (or your equivalent that uses `IOutboxStore`) and publish messages through the MassTransit `ConsumeContext`. The middleware intercepts `Publish`/`Send` and stages the messages alongside the domain write — nothing goes to the broker until `CommitAsync` is called.

```csharp
public class PlaceOrderConsumer : IConsumer<PlaceOrder>
{
    private readonly UnitOfWork _unitOfWork;

    public PlaceOrderConsumer(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task Consume(ConsumeContext<PlaceOrder> context)
    {
        var order = new Order { OrderId = context.Message.OrderId, ... };

        // 1. Stage the domain write.
        _unitOfWork.Add(order);

        // 2. Publish a downstream event — the outbox middleware intercepts this.
        //    The message is NOT sent to the broker yet; it is staged alongside
        //    the domain write so both are committed in one DynamoDB transaction.
        await context.Publish(new OrderPlaced { OrderId = order.OrderId, ... });

        // 3. Atomically: saves Order + outbox message to DynamoDB,
        //    then signals the in-memory channel to dispatch immediately.
        //    If the process crashes here, the recovery job will still dispatch
        //    the OrderPlaced event after the configured threshold.
        await _unitOfWork.CommitAsync(context.CancellationToken);
    }
}
```

## Implementing the Dispatch — Critical Requirement

> **This is the most important implementation detail.** The outbox middleware only *stages* messages in memory. Nothing is written to the database or delivered to the broker until you explicitly dispatch.

### What happens when you call `Publish` or `Send`

The outbox middleware intercepts the call and invokes `IOutboxStore.Add(message)`. At this point the message exists **only in memory** — it has not been written to DynamoDB and has not been sent to RabbitMQ.

```
context.Publish(new OrderPlaced { ... })
  → OutboxSendEndpoint.Send()
    → IOutboxStore.Add(message)   ← in-memory only, not persisted yet
```

### What you must do: call `DispatchMessages()`

You are responsible for persisting the staged messages and triggering dispatch. With the DynamoDB provider this is done through `IDynamoDbStore`, which gives you a DynamoDB transact write containing the outbox messages to combine with your own domain writes:

```csharp
// 1. Get a transact write that includes the staged outbox messages.
var outboxWrite = dynamoDbStore.GetTransactWrite();

// 2. Combine with your domain writes and execute — one atomic DynamoDB transaction.
var txn = new MultiTableTransactWrite();
txn.AddTransactionPart(domainWrite);
txn.AddTransactionPart(outboxWrite);
await txn.ExecuteAsync(ct);

// 3. Signal the in-memory channel so the fast path dispatches immediately.
//    If this call is skipped or the process crashes here, the recovery job
//    will pick up the persisted messages from DynamoDB and dispatch them.
dynamoDbStore.DispatchMessages();
```

Steps 2 and 3 together are what `UnitOfWork.CommitAsync()` does in the sample project. If you never call this (or an equivalent), messages are **silently dropped** — they were never written to DynamoDB and the channel is never notified.

### The `IOutboxStore` lifecycle

`IDynamoDbStore` (and its base `IOutboxStore`) is registered as **scoped**. Each HTTP request or consumer invocation gets its own instance. This means:

- Messages staged in one scope are invisible to other scopes.
- You must call `DispatchMessages()` within the **same scope** that staged the messages.
- Disposing the scope without dispatching discards all staged messages.

### The UnitOfWork pattern

The sample project's `UnitOfWork` is the recommended way to coordinate domain writes with the outbox:

```csharp
public sealed class UnitOfWork : IDisposable
{
    private readonly IDynamoDBContext _context;
    private readonly IDynamoDbStore _dynamoDbStore;   // ← scoped outbox store

    public void Add<T>(T entity) where T : class
    {
        // Stage a domain entity write.
        var write = _context.CreateTransactWrite<T>();
        write.AddSaveItem(entity);
        _transaction.AddTransactionPart(write);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        // Merge outbox messages into the same DynamoDB transaction.
        _transaction.AddTransactionPart(_dynamoDbStore.GetTransactWrite());

        // Atomically write domain data + outbox messages.
        await _transaction.ExecuteAsync(ct);

        // Trigger fast-path dispatch via the in-memory channel.
        _dynamoDbStore.DispatchMessages();
    }
}
```

If `CommitAsync` is never called, or throws before `DispatchMessages()`, the messages that were already written to DynamoDB (if `ExecuteAsync` succeeded) will be retried by the recovery job after `Processing.Threshold`. Messages that were never written to DynamoDB are lost.

## Configuration

All options bind from the `HybridOutbox` configuration section:

```json
{
  "HybridOutbox": {
    "Inbox": {
      "Enabled": true,
    },
    "Job": {
      "Enabled": true,
      "Interval": "00:00:30",
      "Lock": {
        "Duration": "00:01:00"
      }
    },
    "Processing": {
      "Threshold": "00:00:30",
      "BatchSize": 100,
      "Lock": {
        "Duration": "00:01:00"
      }
    },
    "InMemory": {
      "Enabled": true,
      "Capacity": null,
      "DispatchConcurrency": 1
    }
  }
}
```

| Option | Default | Description |
|---|---|---|
| `Inbox.Enabled` | `true` | Enable/disable the inbox |
| `Job.Enabled` | `true` | Enable/disable the background recovery job |
| `Job.Interval` | `30s` | How often the recovery job runs |
| `Job.Lock.Duration` | `60s` | How long a single job instance holds the distributed lock |
| `Processing.Threshold` | `30s` | Minimum message age before the recovery job retries it |
| `Processing.BatchSize` | `100` | Max messages processed per recovery job tick |
| `Processing.Lock.Duration` | `60s` | Per-message lock duration to prevent double-dispatch |
| `InMemory.Enabled` | `true` | Enable/disable the in-memory fast-path channel |
| `InMemory.Capacity` | `null` (unbounded) | Max messages in the in-memory channel |
| `InMemory.DispatchConcurrency` | `1` | Number of parallel dispatch workers |

### Inbox (consumer deduplication)

HybridOutbox supports the inbox pattern to prevent duplicate processing of messages delivered by the broker. When enabled the MassTransit outbox middleware will:

- On consumer entry: if the incoming message has a `MessageId` and the configured `IInboxRepository` reports the id already exists, the message handling is skipped.
- If the id does not exist: an `InboxMessage` is added to the scoped outbox context and persisted together with your domain writes. Once the transaction commits the inbox entry records that the message was processed.

Configuration

- Enable/disable: `HybridOutbox:Inbox:Enabled` (default: `true`).

```csharp
builder.Services
    .AddHybridOutbox(options =>
    {
        options.Inbox.Enabled = false;
    })
```

### DynamoDB options

Bind from the `HybridOutbox:DynamoDb` section or pass a delegate to `AddDynamoDb()`:

| Option | Default | Description |
|---|---|---|
| `TableName` | `"OutboxMessages"` | DynamoDB outbox table name |
| `GsiName` | `"IsProcessed-CreatedAt-index"` | GSI used by the recovery query |
| `RetentionPeriod` | `7 days` | TTL for processed messages (`null` disables TTL) |
| `TtlAttributeName` | `"ExpiresAt"` | DynamoDB TTL attribute name |
| `LockTableName` | `null` | Dedicated table for the distributed job lock. When `null`, a sentinel item in the outbox table is used |

## Distributed Job Lock

In multi-pod deployments, only one instance should run the recovery job at a time. HybridOutbox uses DynamoDB conditional writes to implement a distributed lock:

- The default `NoOpJobLock` (used when only `HybridOutbox` core is registered) always grants the lock — suitable for single-instance deployments.
- `AddDynamoDb()` automatically replaces it with `DynamoDbJobLock`, which uses a sentinel item in DynamoDB to ensure only one pod runs the recovery job per tick.

No extra configuration is needed. Optionally dedicate a separate table for the lock:

```csharp
.AddDynamoDb(options =>
{
    options.TableName = "MyOutbox";
    options.LockTableName = "MyOutboxLock"; // separate table
});
```

## Running the Sample

The `samples/Sample.WebApi` project demonstrates a complete end-to-end flow with RabbitMQ and DynamoDB Local:

```bash
docker-compose up --build
```

| Service | URL |
|---|---|
| Sample Web API | http://localhost:8080 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| DynamoDB Local | http://localhost:8000 |
| DynamoDB Admin | http://localhost:8001 |

### Sample flow

```
POST /orders
  → publishEndpoint.Publish(PlaceOrder)  ← outbox intercepts, not sent yet
  → CommitAsync()  ← PlaceOrder written to outbox table in DynamoDB
    → in-memory channel dispatches PlaceOrder to RabbitMQ
      → PlaceOrderConsumer:
          1. stages Order write in UnitOfWork
          2. context.Publish(OrderPlaced)  ← outbox intercepts again, not sent yet
          3. CommitAsync()  ← Order + OrderPlaced written atomically to DynamoDB
             → in-memory channel dispatches OrderPlaced to RabbitMQ
               → OrderPlacedConsumer writes AuditLog to DynamoDB
        (if pod crashes after step 3, recovery job dispatches OrderPlaced anyway)
```

### Try it

```bash
# Place an order
curl -X POST http://localhost:8080/orders \
  -H "Content-Type: application/json" \
  -d '{"productName": "Widget", "quantity": 2}'

# Check the order was saved by PlaceOrderConsumer
curl http://localhost:8080/orders

# Check the audit log written by OrderPlacedConsumer
# (proves the outbox dispatched the OrderPlaced event)
curl http://localhost:8080/audit-logs
```

## License

MIT — see [LICENSE](LICENSE).
