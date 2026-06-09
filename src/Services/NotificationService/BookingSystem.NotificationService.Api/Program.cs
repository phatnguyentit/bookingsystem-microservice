using BookingSystem.NotificationService.Api.Consumers;
using BookingSystem.NotificationService.Infrastructure.Persistence;
using BookingSystem.NotificationService.Infrastructure.Services;
using BookingSystem.ServiceDefaults;
using BookingSystem.Shared.Messaging;
using BookingSystem.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<NotifDbContext>("notifdb");
builder.AddRedisDistributedCache("redis");

builder.Services.AddKafkaSettings(builder.Configuration);
builder.Services.AddScoped<INotificationSender, EmailNotificationSender>();

// Register Kafka consumers as hosted services
builder.Services.AddHostedService<BookingCreatedKafkaConsumer>();
builder.Services.AddHostedService<PaymentSucceededKafkaConsumer>();
builder.Services.AddHostedService<PaymentFailedKafkaConsumer>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<NotifDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotifDbContext>>();
    await db.MigrateWithRetryAsync(logger, attempts: 5, delay: TimeSpan.FromSeconds(2));
}
else
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation("RunMigrationsOnStartup is false — skipping database migrations.");
}

app.MapDefaultEndpoints();

app.Run();
