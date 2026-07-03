# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Detailed rules

Coding conventions, architectural constraints, and module-specific context are in `.claude/rules/`:

| File | Covers |
|---|---|
| [architecture.md](.claude/rules/architecture.md) | Two service patterns, layer dependency rules, shared projects, adding a new service |
| [api-conventions.md](.claude/rules/api-conventions.md) | Endpoint groups, feature folder layout, command/query naming, response conventions |
| [database.md](.claude/rules/database.md) | EF Core registration, entity configuration, value object mapping, migrations, Redis keys |
| [testing.md](.claude/rules/testing.md) | Test project structure and patterns (BookingService unit tests exist) |
| [modules/identity.md](.claude/rules/modules/identity.md) | UserService — entity, repository, endpoints, gaps |
| [modules/catalog.md](.claude/rules/modules/catalog.md) | CatalogService — entity, repository, endpoint path quirk, gaps |
| [modules/booking.md](.claude/rules/modules/booking.md) | BookingService — DDD aggregate, outbox, commands, Kafka output |
| [modules/payment.md](.claude/rules/modules/payment.md) | PaymentService — entity, command, Kafka output, gaps |
| [modules/notification.md](.claude/rules/modules/notification.md) | NotificationService — Kafka consumers, email sender, no HTTP |
| [modules/search.md](.claude/rules/modules/search.md) | SearchService — Elasticsearch, current filter gaps |
| [modules/review.md](.claude/rules/modules/review.md) | ReviewService — entity, endpoints, no Kafka, gaps |

## Commands

```powershell
# Restore all packages (run from repo root)
dotnet restore

# Build entire solution
dotnet build

# Run everything (services + infra) via Aspire
cd src/Orchestration/BookingSystem.AppHost
dotnet run
# Aspire dashboard: https://localhost:15888

# Infrastructure only (Kafka, Redis, Postgres, Elasticsearch) — no Aspire
docker compose -f docker/docker-compose.infra.yml up -d
```

Unit tests live in `tests/` — one project per service plus `Shared.Messaging.Tests`. Run them with `dotnet test` from the repo root.

### EF Migrations

Run from each service's Infrastructure project (all services with a database have migrations: BookingService, CatalogService, PaymentService, NotificationService, UserService, ReviewService):

```powershell
dotnet ef migrations add <Name> --project src/Services/<ServiceName>/BookingSystem.<ServiceName>Service.Infrastructure
dotnet ef database update       --project src/Services/<ServiceName>/BookingSystem.<ServiceName>Service.Infrastructure
```

At design time the `{Name}DbContextFactory` resolves its connection string via `DesignTimeConnectionString.Resolve(...)` from `Shared.CrossCutting`: the Aspire-injected `ConnectionStrings__{name}db` env var if present, otherwise the `ConnectionStrings:{name}db` entry in the API project's `appsettings.json` (local docker-compose Postgres). So start infra first: `docker compose -f docker/docker-compose.infra.yml up -d`.

Auto-migration at startup is controlled by `RunMigrationsOnStartup` in each service's `appsettings.json`.

## Service map

```
Client → API Gateway (YARP)
  /api/users      → user-service      (userdb)       auth required
  /api/catalog    → catalog-service   (catalogdb)
  /api/bookings   → booking-service   (bookingdb)    auth required
  /api/payments   → payment-service   (paymentdb)    auth required
  /api/search     → search-service    (Elasticsearch)
  /api/reviews    → review-service    (reviewdb)
  NotificationService — Kafka-only, no HTTP          (notifdb)
```

In `Development` the gateway uses a pass-through auth handler — no real JWT required locally.

> Note: `docs/architecture-patterns.md` lists the Outbox Pattern as a gap — it is fully implemented in BookingService.
