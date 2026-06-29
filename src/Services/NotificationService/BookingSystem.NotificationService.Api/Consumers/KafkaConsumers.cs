using BookingSystem.NotificationService.Infrastructure.Services;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Contracts.Events.Bookings;
using BookingSystem.Shared.Contracts.Events.Payments;
using BookingSystem.Shared.Messaging;
using Microsoft.Extensions.Options;

namespace BookingSystem.NotificationService.Api.Consumers;

public class BookingCreatedKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<BookingCreatedKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<BookingCreatedIntegrationEvent>(
        KafkaTopics.BookingCreated, $"notification-service-{KafkaTopics.BookingCreated}", kafkaSettings, logger)
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

public class BookingCancelledKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<BookingCancelledKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<BookingCancelledIntegrationEvent>(
        KafkaTopics.BookingCancelled, $"notification-service-{KafkaTopics.BookingCancelled}", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(BookingCancelledIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        await sender.SendEmailAsync(
            message.UserId,
            $"Your booking {message.BookingId} has been cancelled. Reason: {message.Reason}.", cancellationToken);
    }
}

public class PaymentSucceededKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<PaymentSucceededKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentSucceededIntegrationEvent>(
        KafkaTopics.PaymentSucceeded, $"notification-service-{KafkaTopics.PaymentSucceeded}", kafkaSettings, logger)
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
        KafkaTopics.PaymentFailed, $"notification-service-{KafkaTopics.PaymentFailed}", kafkaSettings, logger)
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
