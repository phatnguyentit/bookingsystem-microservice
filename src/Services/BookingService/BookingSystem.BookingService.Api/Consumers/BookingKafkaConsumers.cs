using BookingSystem.BookingService.Application.Commands.CancelBooking;
using BookingSystem.BookingService.Application.Commands.ConfirmBooking;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.Options;

namespace BookingSystem.BookingService.Api.Consumers;

public class PaymentSucceededKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<PaymentSucceededKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentSucceededIntegrationEvent>(
        KafkaTopics.PaymentSucceeded, $"booking-service-{KafkaTopics.PaymentSucceeded}", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(PaymentSucceededIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new ConfirmBookingCommand(message.BookingId), cancellationToken);
    }
}

public class PaymentFailedKafkaConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<PaymentFailedKafkaConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentFailedIntegrationEvent>(
        KafkaTopics.PaymentFailed, $"booking-service-{KafkaTopics.PaymentFailed}", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(PaymentFailedIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CancelBookingCommand(message.BookingId, $"Payment failed: {message.Reason}"), cancellationToken);
    }
}
