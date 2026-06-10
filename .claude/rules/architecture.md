# Architecture Rules

## Two service patterns

There are exactly two structural patterns in this repo. Choose based on domain complexity.

### Pattern A — 4-layer Clean Architecture (BookingService only)
Use when the domain has complex business rules, aggregates, or state machines.

```
BookingSystem.{Name}Service.Domain/         ← pure C#, zero framework refs
BookingSystem.{Name}Service.Application/    ← MediatR, interfaces only
BookingSystem.{Name}Service.Infrastructure/ ← EF Core, Kafka, HTTP clients
BookingSystem.{Name}Service.Api/            ← Minimal API, DI wiring
```

Dependency rule: each layer may only reference layers to its left. `Domain` has no project references at all. `Application` references `Domain` only. `Infrastructure` references `Application` + `Domain`. `Api` references all.

### Pattern B — 2-layer vertical slice (all other services)
Use when the service has CRUD-style operations with no domain logic.

```
BookingSystem.{Name}Service.Api/            ← Features/, Endpoints/, Program.cs
BookingSystem.{Name}Service.Infrastructure/ ← Persistence/, Repositories/
```

`Api` references `Infrastructure` and `Shared.*` projects only.

## Shared projects

| Project | Purpose | What goes here |
|---|---|---|
| `Shared.Contracts` | Cross-service data shapes | Integration event records, DTOs shared by multiple services |
| `Shared.Messaging` | Kafka abstraction | `IEventPublisher`, `KafkaEventPublisher`, `KafkaConsumerBase<T>`, `KafkaServerSettings` |
| `Shared.Persistence` | EF helpers | `MigrateWithRetryAsync` extension only |
| `ServiceDefaults` | Aspire defaults | `AddServiceDefaults()` — OTel, health checks, service discovery, resilience |

Never add business logic to Shared projects. Never add a service-specific type to `Shared.Contracts` unless at least two services consume it.

## Cross-service communication

**Synchronous (HTTP):** Only BookingService calls other services at request time (CatalogService + UserService via typed `HttpClient`). All HTTP clients are registered with Aspire service discovery (`http://{service-name}`) and get `StandardResilienceHandler` automatically from `ServiceDefaults`.

**Asynchronous (Kafka):** All other inter-service communication is event-driven. Producers publish integration events from `Shared.Contracts.Events`. Consumers are `BackgroundService` implementations registered via `AddHostedService`.

No service reads another service's database.

## Aspire orchestration

All infrastructure (Postgres, Redis, Kafka, Elasticsearch) and all service projects are declared in `BookingSystem.AppHost/Program.cs`. Kafka topics are provisioned in an `Eventing.Subscribe<ResourceReadyEvent>` callback — do not rely on Kafka auto-creation. The `RunMigrationsOnStartup` appsettings flag controls whether each service runs `MigrateWithRetryAsync` on boot.

## Adding a new service

1. Create `BookingSystem.{Name}Service.Api` + `BookingSystem.{Name}Service.Infrastructure` projects.
2. Call `builder.AddServiceDefaults()` first in `Program.cs`.
3. Register `AddNpgsqlDbContext<{Name}DbContext>("{name}db")`.
4. Add the service and its database to `AppHost/Program.cs`.
5. Add a YARP route + cluster entry in `ApiGateway/appsettings.json`.
6. Create an `IDesignTimeDbContextFactory<{Name}DbContext>` in the Infrastructure project for `dotnet ef` CLI support.
