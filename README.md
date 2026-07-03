[![Build](https://github.com/phatnguyentit/bookingsystem-microservice/actions/workflows/dotnet.yml/badge.svg)](https://github.com/phatnguyentit/bookingsystem-microservice/actions/workflows/cicd.yml)
[![CodeQL Advanced](https://github.com/phatnguyentit/bookingsystem-microservice/actions/workflows/codeql.yml/badge.svg)](https://github.com/phatnguyentit/bookingsystem-microservice/actions/workflows/codeql.yml)
[![Tests](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/phatnguyentit/3c5ef9cca029a25a961494cb73c9f22a/raw/bookingsystem-tests.json)](https://github.com/phatnguyentit/bookingsystem-microservice/actions/workflows/dotnet.yml)
# Booking System Microservice

A .NET 10 microservices booking platform demonstrating CQRS, DDD, Kafka event choreography, and distributed system patterns.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# |
| Orchestration | .NET Aspire 9+ |
| API Gateway | YARP (reverse proxy, JWT auth, rate limiting) |
| Services | ASP.NET Core Minimal API |
| ORM | Entity Framework Core 10 |
| CQRS | MediatR |
| Database | PostgreSQL (per-service) |
| Cache / Lock | Redis (distributed cache + Redlock) |
| Messaging | Kafka (Confluent.Kafka) |
| Search | Elasticsearch |
| Observability | OpenTelemetry (traces, metrics, logs) |

---

## Architecture

7 independent services communicate via Kafka events, routed through a YARP API gateway, and orchestrated with .NET Aspire.

```
Client
  ↓
API Gateway (YARP) — JWT auth, rate limit (100 req/min)
  ↓
┌──────────────┬────────────────┬───────────────┬──────────────────┐
│ UserService  │ CatalogService │ BookingService │ PaymentService   │
│              │                │ (DDD/CQRS)    │                  │
└──────────────┴────────────────┴───────────────┴──────────────────┘
       ↓ Kafka events (booking.created, payment.succeeded, …)
┌──────────────────┬──────────────┬───────────────┐
│ NotificationSvc  │ SearchService│ ReviewService  │
└──────────────────┴──────────────┴───────────────┘
```

### Key Patterns

- **Database-per-Service** — isolated PostgreSQL databases (`userdb`, `catalogdb`, `bookingdb`, …)
- **CQRS via MediatR** — commands and queries separated across all services
- **DDD on BookingService** — aggregate root, value objects (`Money`, `DateRange`, `BookingId`), domain events, repository pattern
- **Event-Driven Choreography** — domain events translate to Kafka integration events; services subscribe independently
- **Outbox Pattern** — `UnitOfWork` saves to DB then dispatches domain events, keeping persistence and messaging in sync
- **Distributed Locking** — Redis locks on `lock:listing:{id}:{date}` prevent double-booking race conditions
- **Resilience** — `StandardResilienceHandler` (retry, circuit breaker, timeout) on all HTTP clients
- **Service Discovery** — .NET Aspire DNS-based; no hardcoded ports

### Booking State Machine

```
Pending → Confirmed → Completed
       ↘ Cancelled
```

### Kafka Topics

| Topic | Producer | Consumers |
|---|---|---|
| `booking.created` | BookingService | PaymentService, NotificationService, CatalogService |
| `booking.cancelled` | BookingService | NotificationService, CatalogService |
| `payment.succeeded` | PaymentService | BookingService, NotificationService |
| `payment.failed` | PaymentService | BookingService, NotificationService |
| `catalog.availability.updated` | CatalogService | SearchService |

---

## Solution Structure

```
src/
├── Orchestration/BookingSystem.AppHost/       # .NET Aspire host
├── ServiceDefaults/BookingSystem.ServiceDefaults/
├── Gateway/BookingSystem.ApiGateway/          # YARP
└── Services/
    ├── UserService/
    ├── CatalogService/
    ├── BookingService/                        # DDD — Domain / Application / Infrastructure / Api
    ├── PaymentService/
    ├── NotificationService/
    ├── SearchService/
    └── ReviewService/
docker/
└── docker-compose.infra.yml                  # Kafka, Redis, Postgres, Elasticsearch
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop
- .NET Aspire workload

```bash
dotnet workload install aspire
dotnet tool install --global dotnet-ef
```

### Run

```bash
git clone https://github.com/your-org/bookingsystem-microservice.git
cd bookingsystem-microservice
dotnet restore

# Start all services + infrastructure via Aspire
cd src/Orchestration/BookingSystem.AppHost
dotnet run
```

### Infrastructure Only (without Aspire)

```bash
docker compose -f docker/docker-compose.infra.yml up -d
```

### Migrations

Run from each service's Infrastructure project:

```bash
dotnet ef migrations add InitialCreate --project BookingSystem.BookingService.Infrastructure
dotnet ef database update
```

---

## Docs

- [Architecture & Design Patterns](docs/architecture-patterns.md)
- [Booking System Guide](docs/booking-system-guide.md)
