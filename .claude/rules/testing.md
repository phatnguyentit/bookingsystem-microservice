# Testing

## Current state

Unit test projects exist for every service and for `Shared.Messaging` (issue #38). Integration test projects (Testcontainers) have not been added yet.

## Project structure

```
tests/
├── BookingService.Domain.Tests/        ← aggregate, value objects, events; no EF/Kafka deps
├── BookingService.Application.Tests/   ← command/query/event handlers (NSubstitute mocks)
├── BookingService.Api.Tests/           ← Kafka consumer → command dispatch
├── UserService.Tests/                  ← feature handlers
├── CatalogService.Tests/               ← feature handlers
├── PaymentService.Tests/               ← ProcessPayment/RefundPayment handlers + consumers
├── ReviewService.Tests/                ← rating validation
├── SearchService.Tests/                ← query delegation
├── NotificationService.Tests/          ← consumers + EmailNotificationSender (Sqlite in-memory)
├── Shared.Messaging.Tests/             ← KafkaConsumerBase retry/dead-letter logic
└── {Service}.Integration.Tests/        ← Testcontainers (Postgres, Kafka, Redis) — not yet added
```

Conventions: all projects target `net10.0`, use xUnit + FluentAssertions (pinned 7.x, the last Apache-2.0 release) + NSubstitute, declare `<Using Include="Xunit" />` (no `using Xunit;` needed in files), and are registered in **both** solutions: `BookingSystem.slnx` (everything) and `BookingSystem.Tests.slnx` (tests only — use `dotnet test BookingSystem.Tests.slnx` for a faster test-focused loop). Projects that reference an `*.Api` project also need `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

## Testing Kafka consumers

`KafkaConsumerBase<T>` exposes two `protected virtual` factory seams — `CreateConsumer(ConsumerConfig)` and `CreateDeadLetterProducer(ProducerConfig)` — so its consume/retry/dead-letter loop is unit-testable with substituted `IConsumer`/`IProducer` (see `Shared.Messaging.Tests/KafkaConsumerBaseTests.cs`; a queue of `ConsumeResult`s drives the loop, and draining it throws `OperationCanceledException` to stop it).

Concrete consumers' `ProcessAsync` overrides are tested via a test subclass that exposes the protected method, with a real `ServiceCollection` container holding a substituted `ISender`/`INotificationSender` — this exercises the scope-resolution path without mocking DI extension methods.

## Unit tests — Domain layer

Domain tests have zero infrastructure dependencies. Test aggregate behavior, value object invariants, and domain exceptions directly.

```csharp
public class BookingTests
{
    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsBookingDomainException()
    {
        var booking = BookingFaker.Confirmed();
        booking.Cancel("first reason");

        var act = () => booking.Cancel("second reason");

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Create_RaisesDomainEvent_BookingCreatedEvent()
    {
        var booking = Booking.Create(...);

        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingCreatedEvent>();
    }
}
```

Naming: `{Subject}_{Condition}_{ExpectedOutcome}`.

## Unit tests — Application layer (Pattern B services)

Mock repositories with NSubstitute. No EF Core, no Kafka.

```csharp
public class CreateUserHandlerTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_ValidCommand_ReturnsNewGuid()
    {
        var handler = new CreateUserHandler(_repo);
        var result = await handler.Handle(new CreateUserCommand("a@b.com", "Alice", "hash"), default);

        result.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<User>(), default);
    }
}
```

## Integration tests

Use `Testcontainers` for real Postgres. Build the `DbContext` directly with the container's connection string — the design-time `IDesignTimeDbContextFactory` ignores its `args` and resolves from the Aspire env var / API `appsettings.json`, so it is not suitable for tests. Run migrations, seed data, then exercise repository or handler logic.

```csharp
public class BookingRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var ctx = new BookingDbContext(options);
        await ctx.Database.MigrateAsync();
    }
}
```

## CI

The existing `dotnet.yml` workflow runs `dotnet test --no-build --verbosity normal`. No additional test configuration is needed; discovered xUnit projects are picked up automatically.
