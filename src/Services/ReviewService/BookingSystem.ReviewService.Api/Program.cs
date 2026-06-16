using BookingSystem.ReviewService.Api.Endpoints;
using BookingSystem.ReviewService.Infrastructure.Persistence;
using BookingSystem.ServiceDefaults;
using BookingSystem.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ReviewDbContext>("reviewdb");

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddScoped<IReviewRepository, ReviewRepository>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ReviewDbContext>>();
    await db.MigrateWithRetryAsync(logger, attempts: 5, delay: TimeSpan.FromSeconds(2));
}

app.MapDefaultEndpoints();
app.MapReviewEndpoints();

app.Run();
