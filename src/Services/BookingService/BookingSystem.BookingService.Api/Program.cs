using BookingSystem.BookingService.Api.Consumers;
using BookingSystem.BookingService.Api.Endpoints;
using BookingSystem.Shared.Persistence;
using BookingSystem.BookingService.Application.Commands.CreateBooking;
using BookingSystem.BookingService.Application.Interfaces;
using BookingSystem.BookingService.Application.Interfaces.UoW;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Infrastructure.HttpClients;
using BookingSystem.BookingService.Infrastructure.Messaging;
using BookingSystem.BookingService.Infrastructure.Outbox;
using BookingSystem.BookingService.Infrastructure.Persistence;
using BookingSystem.BookingService.Infrastructure.Repositories;
using BookingSystem.ServiceDefaults;
using BookingSystem.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<BookingDbContext>("bookingdb");
builder.AddRedisDistributedCache("redis");

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateBookingCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddHostedService<OutboxProcessor>();

builder.Services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(
    c => c.BaseAddress = new Uri("http://catalog-service"));
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(
    c => c.BaseAddress = new Uri("http://user-service"));

builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddHostedService<PaymentSucceededKafkaConsumer>();
builder.Services.AddHostedService<PaymentFailedKafkaConsumer>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<BookingDbContext>>();
    await db.MigrateWithRetryAsync(logger, attempts: 5, delay: TimeSpan.FromSeconds(2));
}
else
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation("RunMigrationsOnStartup is false — skipping database migrations.");
}

app.MapDefaultEndpoints();
app.MapBookingEndpoints();

app.Run();