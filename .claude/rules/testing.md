# Testing

## Current state

There are no test projects in the repository. The CI workflow (`dotnet test`) will no-op until test projects are added.

## Recommended project structure (when adding tests)

```
tests/
├── BookingService.Domain.Tests/        ← xUnit, no EF/Kafka deps
├── BookingService.Application.Tests/   ← xUnit + NSubstitute
├── BookingService.Integration.Tests/   ← Testcontainers (Postgres, Kafka, Redis)
└── {OtherService}.Integration.Tests/   ← per service as needed
```

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

Use `Testcontainers` for real Postgres. Spin up an `IDesignTimeDbContextFactory` instance, run migrations, seed data, then exercise repository or handler logic.

```csharp
public class BookingRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var ctx = new BookingDbContextFactory().CreateDbContext([_postgres.GetConnectionString()]);
        await ctx.Database.MigrateAsync();
    }
}
```

## CI

The existing `dotnet.yml` workflow runs `dotnet test --no-build --verbosity normal`. No additional test configuration is needed; discovered xUnit projects are picked up automatically.
