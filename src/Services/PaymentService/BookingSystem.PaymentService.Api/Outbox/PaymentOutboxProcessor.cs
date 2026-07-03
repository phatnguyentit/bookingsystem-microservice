using BookingSystem.PaymentService.Infrastructure.Outbox;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.Shared.Contracts.Events.Payments;
using BookingSystem.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookingSystem.PaymentService.Api.Outbox;

/// <summary>
/// Polls the outbox table and publishes pending integration events to Kafka. This decouples
/// the database commit from the Kafka publish: the event row is written atomically with the
/// payment state change (see <c>ProcessPaymentHandler</c>), and delivery is retried here on
/// each cycle until it succeeds — so a Kafka outage can never silently drop an outcome event.
/// </summary>
public class PaymentOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentOutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error in PaymentOutboxProcessor polling loop");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Error == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(publisher, message, ct);
                message.MarkProcessed();

                logger.LogInformation(
                    "Published outbox message {Id} ({EventType}) to {Topic}",
                    message.Id, message.EventType, message.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to publish outbox message {Id} ({EventType})",
                    message.Id, message.EventType);

                message.MarkFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Task PublishAsync(IEventPublisher publisher, OutboxMessage message, CancellationToken ct) =>
        message.EventType switch
        {
            nameof(PaymentSucceededIntegrationEvent) => publisher.PublishAsync(
                message.Topic,
                JsonSerializer.Deserialize<PaymentSucceededIntegrationEvent>(message.Payload)!, ct),
            nameof(PaymentFailedIntegrationEvent) => publisher.PublishAsync(
                message.Topic,
                JsonSerializer.Deserialize<PaymentFailedIntegrationEvent>(message.Payload)!, ct),
            nameof(PaymentRefundedIntegrationEvent) => publisher.PublishAsync(
                message.Topic,
                JsonSerializer.Deserialize<PaymentRefundedIntegrationEvent>(message.Payload)!, ct),
            nameof(PaymentRefundFailedIntegrationEvent) => publisher.PublishAsync(
                message.Topic,
                JsonSerializer.Deserialize<PaymentRefundFailedIntegrationEvent>(message.Payload)!, ct),
            _ => throw new InvalidOperationException($"Unknown outbox event type '{message.EventType}'.")
        };
}
