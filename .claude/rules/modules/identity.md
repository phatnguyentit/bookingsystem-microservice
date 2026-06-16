# Identity Module — UserService

## Location
`src/Services/UserService/`

## Structure (Pattern B — 2-layer)
```
BookingSystem.UserService.Api/
├── Program.cs
├── Endpoints/UserEndpoints.cs
└── Features/
    ├── Create/
    │   ├── CreateUserCommand.cs   ← command + handler in same file
    │   └── CreateUserHandler.cs
    └── GetById/
        ├── GetUserByIdQuery.cs    ← query + handler + UserDto in same file
        └── GetUserByIdHandler.cs

BookingSystem.UserService.Infrastructure/
└── Persistence/
    ├── UserDbContext.cs           ← User entity defined here
    ├── UserDbContextFactory.cs    ← IDesignTimeDbContextFactory for dotnet ef
    ├── Migrations/
    └── Repositories/
        ├── IUserRepository.cs
        └── UserRepository.cs
```

## Entity

Defined in `UserDbContext.cs` (same file as the DbContext):

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;      // max 256, unique index
    public string FullName { get; set; } = string.Empty;   // max 200
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

Table name: `users`; columns mapped to `snake_case` (`full_name`, `password_hash`, `created_at`). Configuration is inline in `OnModelCreating`.

## Repository

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
```

`UserRepository.AddAsync` calls `SaveChangesAsync` directly — there is no separate `UnitOfWork`.

## DTO

`UserDto` is defined in `GetUserByIdQuery.cs` (not in a shared Contracts project):

```csharp
public record UserDto(Guid Id, string Email, string FullName, DateTime CreatedAt);
```

## Commands and queries

```csharp
public record CreateUserCommand(string Email, string FullName, string PasswordHash) : IRequest<Guid>;
public record GetUserByIdQuery(Guid UserId) : IRequest<UserDto?>;
```

## Endpoints

| Method | Path | Handler | Auth (gateway) |
|---|---|---|---|
| POST | `/api/users` | `CreateUserCommand` | Required |
| GET | `/api/users/{id:guid}` | `GetUserByIdQuery` | Required |

Returns `Results.NotFound()` when query returns `null`.

## DI registration (Program.cs)

```csharp
builder.AddNpgsqlDbContext<UserDbContext>("userdb");
builder.AddRedisDistributedCache("redis");
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

Has a `RunMigrationsOnStartup` block using `MigrateWithRetryAsync` (set to `true` in `appsettings.json`).

## How BookingService uses this service

`IUserServiceClient` (Infrastructure/HttpClients) calls `GET /api/users/{userId}` and returns `true` if the response is a success status code. The interface is `Task<bool> UserExistsAsync(Guid userId, ...)`. Currently, `CreateBookingHandler` does **not** call `UserExistsAsync` — the dependency is registered but the handler only calls `ICatalogServiceClient`.

## Gaps

- No email uniqueness check at the command layer (DB has a unique index; violation surfaces as an exception)
- No profile update or password change endpoint
