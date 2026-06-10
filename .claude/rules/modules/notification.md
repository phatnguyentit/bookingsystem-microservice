# Notification Module — NotificationService

## Location
`src/Services/NotificationService/`

## Structure (Pattern B — 2-layer, no HTTP endpoints)
```
BookingSystem.NotificationService.Api/
├── Program.cs
└── Consumers/KafkaConsumers.cs   ← KafkaConsumerBase<T> + all 3 consumers

BookingSystem.NotificationService.Infrastructure/
└── Persistence/
    ├── NotifDbContext.cs          ← NotificationLog entity defined here
    ├── NotifDbContextFactory.cs
    ├── Migrations/
    └── Services/EmailSender.cs   ← INotificationSender + EmailNotificationSender
```

## No HTTP endpoints

NotificationService exposes no REST API. It is triggered entirely by Kafka events. `app.MapDefaultEndpoints()` is called (health + metrics only).

## Kafka consumers

All consumers are in `KafkaConsumers.cs`. They share a base class:

```csharp
public abstract class KafkaConsumerBase<T>(
    string topic,
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger logger) : BackgroundService where T : class
{
    protected abstract Task ProcessAsync(T message, CancellationToken cancellationToken);
}
```

Consumer config:
- `GroupId`: `notification-service-{topic}` (e.g. `notification-service-booking.created`)
- `AutoOffsetReset`: `Earliest`
- `EnableAutoCommit`: `false` — offset committed manually after successful `ProcessAsync`
- Failed deserialization or `ProcessAsync` exceptions are logged and the message is skipped (offset still committed)

Registered consumers:

| Class | Topic | Message type | Action |
|---|---|---|---|
| `BookingCreatedKafkaConsumer` | `booking.created` | `BookingCreatedIntegrationEvent` | Sends "booking created" email |
| `PaymentSucceededKafkaConsumer` | `payment.succeeded` | `PaymentSucceededIntegrationEvent` | Sends "payment succeeded" email |
| `PaymentFailedKafkaConsumer` | `payment.failed` | `PaymentFailedIntegrationEvent` | Sends "payment failed" email |

Each consumer creates a new `IServiceScopeFactory` scope to resolve `INotificationSender` (scoped service).

## Notification sender

```csharp
public interface INotificationSender
{
    Task SendEmailAsync(Guid recipientId, string message, CancellationToken cancellationToken = default);
}
```

`EmailNotificationSender`:
- Logs the email via `ILogger`
- Persists a `NotificationLog` row to `notifdb`
- No actual SMTP/SendGrid integration (placeholder)

## Entity

```csharp
public class NotificationLog
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string Message { get; set; } = string.Empty;  // max 1000
    public string Channel { get; set; } = "Email";        // max 20
    public bool IsDelivered { get; set; }
    public DateTime SentAt { get; set; }
}
```

Table name: `notification_logs`. Configuration inline in `OnModelCreating`.

## DI registration (Program.cs)

```csharp
builder.AddNpgsqlDbContext<NotifDbContext>("notifdb");
builder.AddRedisDistributedCache("redis");
builder.Services.AddKafkaSettings(builder.Configuration);   // note: AddKafkaSettings, not AddKafkaMessaging
builder.Services.AddScoped<INotificationSender, EmailNotificationSender>();
builder.Services.AddHostedService<BookingCreatedKafkaConsumer>();
builder.Services.AddHostedService<PaymentSucceededKafkaConsumer>();
builder.Services.AddHostedService<PaymentFailedKafkaConsumer>();
```

Uses `AddKafkaSettings` (reads config only, no producer) rather than `AddKafkaMessaging` (which also wires the `IEventPublisher` producer).

Has `RunMigrationsOnStartup` block using `MigrateWithRetryAsync`.

## Adding a new consumer

1. Add a new class in `KafkaConsumers.cs` inheriting `KafkaConsumerBase<TEvent>`
2. Register with `builder.Services.AddHostedService<YourNewConsumer>()`
3. Add the integration event type to `Shared.Contracts.Events` if it doesn't exist

## Gaps

- No real email delivery (SMTP/SendGrid/SES integration is a TODO in `EmailNotificationSender`)
- `booking.cancelled` topic exists in architecture but has no consumer here
- `RecipientId` stored as `Guid` — no lookup to get the actual email address
