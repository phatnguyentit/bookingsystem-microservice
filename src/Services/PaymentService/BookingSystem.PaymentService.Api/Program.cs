using BookingSystem.PaymentService.Api.Consumers;
using BookingSystem.PaymentService.Api.Endpoints;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.ServiceDefaults;
using BookingSystem.Shared.Messaging;
using BookingSystem.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PaymentDbContext>("paymentdb");

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddKafkaMessaging(builder.Configuration);

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddHostedService<BookingCreatedPaymentConsumer>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentDbContext>>();
    await db.MigrateWithRetryAsync(logger, attempts: 5, delay: TimeSpan.FromSeconds(2));
}
else
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation("RunMigrationsOnStartup is false — skipping database migrations.");
}

app.MapDefaultEndpoints();
app.MapPaymentEndpoints();

app.Run();
