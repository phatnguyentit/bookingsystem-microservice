# API Conventions

## Endpoint registration

All endpoints use Minimal API route groups via an extension method on `WebApplication`. The method is called from `Program.cs` after `app.MapDefaultEndpoints()`.

```csharp
// Endpoints/{Resource}Endpoints.cs
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users");

        group.MapPost("/", async (CreateUserCommand cmd, ISender sender) =>
        {
            var id = await sender.Send(cmd);
            return Results.Created($"/api/users/{id}", new { id });
        })
        .WithName("CreateUser");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var dto = await sender.Send(new GetUserByIdQuery(id));
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .WithName("GetUser");
    }
}
```

Rules:
- Group path: `/api/{resource}` (plural, lowercase)
- `WithName`: PascalCase verb + noun — `"CreateUser"`, `"GetBooking"`, `"CancelBooking"`
- Always inject `ISender` (not `IMediator`) for dispatch
- Add `.RequireAuthorization()` on the group or individual route as needed; do not add it globally at the group level unless all routes require auth

## Feature folder layout (Pattern B services)

```
Features/
├── Create/
│   ├── CreateUserCommand.cs       ← record + IRequest<T>
│   └── CreateUserHandler.cs       ← IRequestHandler<TCommand, T>
└── GetById/
    ├── GetUserByIdQuery.cs        ← record + IRequest<TDto?>
    └── GetUserByIdHandler.cs      ← IRequestHandler<TQuery, TDto?>
```

For Pattern A (BookingService), commands and queries live in the `Application` project under `Commands/{Action}/` and `Queries/{Action}/`.

## Command and query naming

| Type | Format | Return |
|---|---|---|
| Command (mutating) | `Create{Resource}Command`, `Cancel{Resource}Command` | `IRequest<Guid>` or `IRequest` |
| Query | `Get{Resource}ByIdQuery`, `Get{Resource}Query` | `IRequest<{Resource}Dto?>` |
| Handler | `{CommandOrQuery}Handler` | `IRequestHandler<TRequest, TResponse>` |

Commands and queries are `record` types. Handlers use primary constructor injection.

```csharp
public record CreateUserCommand(string Email, string FullName, string PasswordHash) : IRequest<Guid>;

public class CreateUserHandler(IUserRepository repo) : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand cmd, CancellationToken cancellationToken)
    {
        var user = new User { Id = Guid.NewGuid(), Email = cmd.Email, ... };
        await repo.AddAsync(user, cancellationToken);
        return user.Id;
    }
}
```

## HTTP response conventions

| Situation | Result |
|---|---|
| Resource created | `Results.Created($"/api/{resource}/{id}", new {{ id }})` |
| Resource found | `Results.Ok(dto)` |
| Resource not found | `Results.NotFound()` |
| Command with no return value | `Results.NoContent()` |
| Validation failure | Throw from handler; map to `Results.BadRequest` at endpoint or via exception filter |

## MediatR registration

Register MediatR in `Program.cs` with all relevant assemblies:

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateUserCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
```

For Pattern B services the command/query types live in the `Api` assembly, so one `typeof(Program).Assembly` call usually suffices. For BookingService both `Application` and `Api` assemblies must be registered.

## Authentication at the gateway

JWT auth is only enforced in non-Development environments. In Development, a pass-through `AuthHandler` is registered. Routes in `ApiGateway/appsettings.json` with `"AuthorizationPolicy": "default"` require a valid token in production; routes without that property are unauthenticated.

Currently authenticated routes: `user-route`, `booking-route`, `payment-route`.
Currently unauthenticated: `catalog-route`, `search-route`, `review-route`.
