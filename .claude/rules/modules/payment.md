# Payment Module — PaymentService

## Location
`src/Services/PaymentService/`

## Structure (Pattern B — 2-layer)
```
BookingSystem.PaymentService.Api/
├── Program.cs
├── Endpoints/PaymentEndpoints.cs
└── Features/ProcessPayment/
    ├── ProcessPaymentCommand.cs
    └── ProcessPaymentHandler.cs

BookingSystem.PaymentService.Infrastructure/
└── Persistence/
    ├── PaymentDbContext.cs       ← Payment entity + PaymentId defined here
    ├── PaymentDbContextFactory.cs
    ├── PaymentId.cs
    ├── IPaymentRepository.cs
    ├── PaymentRepository.cs
    └── Migrations/
```

## Entity and value object

Both `Payment` and `PaymentId` are defined in the Infrastructure project (unlike BookingService where value objects live in Domain):

```csharp
public class Payment
{
    public PaymentId Id { get; set; } = default!;   // strongly-typed value object
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }              // decimal(18,2)
    public string Currency { get; set; } = "USD";   // max 3
    public string Status { get; set; } = string.Empty;  // max 20
    public DateTime CreatedAt { get; set; }
}

public record PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
```

Table name: `payments`. EF config is inline in `OnModelCreating`:
```csharp
e.Property(p => p.Id).HasConversion(id => id.Value, v => new PaymentId(v));
```

## Repository

```csharp
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
}
```

`PaymentRepository.AddAsync` calls `SaveChangesAsync` directly — no `UnitOfWork`.

## Command

```csharp
public record ProcessPaymentCommand(
    Guid BookingId, Guid UserId,
    decimal Amount, string Currency,
    string PaymentMethod) : IRequest<Guid>;
```

`ProcessPaymentHandler`:
1. Creates a `Payment` with status `"Succeeded"` (simplified — no real payment gateway call)
2. Calls `repo.AddAsync`
3. Publishes `payment.succeeded` to Kafka via `IEventPublisher`
4. Returns `payment.Id.Value` (raw `Guid`)

There is **no** `payment.failed` path in `ProcessPaymentHandler` — the failure event shape exists in `Shared.Contracts` but is not published by any current code.

## Endpoints

| Method | Path | Handler | Auth (gateway) |
|---|---|---|---|
| POST | `/api/payments` | `ProcessPaymentCommand` | Required |
| GET | `/api/payments/{id:guid}` | Direct `IPaymentRepository` call | Required |

`GET` does not go through MediatR — the endpoint injects `IPaymentRepository` directly and returns `Results.NotFound()` on null.

## Kafka

Publishes `payment.succeeded` (`PaymentSucceededIntegrationEvent`) after a successful payment.

```csharp
new PaymentSucceededIntegrationEvent(
    payment.Id.Value, payment.BookingId, payment.UserId,
    payment.Amount, payment.Currency, DateTime.UtcNow)
```

**No Kafka consumer** — PaymentService does not listen to `booking.created` despite architecture documentation suggesting it should. The flow that triggers payment processing currently requires a direct `POST /api/payments` call.

## DI registration (Program.cs)

```csharp
builder.AddNpgsqlDbContext<PaymentDbContext>("paymentdb");
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
```

Has `RunMigrationsOnStartup` block using `MigrateWithRetryAsync`.

## Gaps

- No Kafka consumer for `booking.created` — payment is not triggered automatically by a booking
- `payment.failed` event is never published
- `ConfirmBookingCommand` in BookingService exists but has no consumer for `payment.succeeded`
- `Status` is hardcoded to `"Succeeded"` — no payment gateway integration
