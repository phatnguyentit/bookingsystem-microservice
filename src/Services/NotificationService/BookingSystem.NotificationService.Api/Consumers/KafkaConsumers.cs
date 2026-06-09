using BookingSystem.NotificationService.Infrastructure.Services;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BookingSystem.NotificationService.Api.Consumers;

public abstract class KafkaConsumerBase<T>(
    string topic,
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger logger) : BackgroundService where T : class
{
    protected abstract Task ProcessAsync(T message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = $"notification-service-{topic}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                try
                {
                    var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                    if (message is not null)
                        await ProcessAsync(message, stoppingToken);
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message from topic {Topic}", topic);
                }
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
        finally
        {
            consumer.Close();
        }
    }
}

public class BookingCreatedKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<BookingCreatedKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<BookingCreatedIntegrationEvent>(
        "booking.created", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(BookingCreatedIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        await sender.SendEmailAsync(
            message.UserId,
            $"Your booking {message.BookingId} has been created!", cancellationToken);
    }
}

public class PaymentSucceededKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<PaymentSucceededKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentSucceededIntegrationEvent>(
        "payment.succeeded", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(
        PaymentSucceededIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        await sender.SendEmailAsync(
            message.UserId,
            $"Payment of {message.Amount} {message.Currency} for booking {message.BookingId} succeeded.", cancellationToken);
    }
}

public class PaymentFailedKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<PaymentFailedKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentFailedIntegrationEvent>(
        "payment.failed", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(
        PaymentFailedIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        await sender.SendEmailAsync(
            message.UserId,
            $"Payment for booking {message.BookingId} failed: {message.Reason}.", cancellationToken);
    }
}
