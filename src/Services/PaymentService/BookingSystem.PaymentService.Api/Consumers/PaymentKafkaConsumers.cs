using BookingSystem.PaymentService.Api.Features.ProcessPayment;
using BookingSystem.PaymentService.Api.Features.RefundPayment;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.Options;

namespace BookingSystem.PaymentService.Api.Consumers;

public class BookingCreatedPaymentConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<BookingCreatedPaymentConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<BookingCreatedIntegrationEvent>(
        KafkaTopics.BookingCreated, $"payment-service-{KafkaTopics.BookingCreated}", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(BookingCreatedIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ProcessPaymentCommand(
            BookingId: message.BookingId,
            UserId: message.UserId,
            Amount: message.Amount,
            Currency: message.Currency,
            PaymentMethod: "Card"), cancellationToken);
    }
}

public class BookingConfirmationFailedPaymentConsumer(
    IOptions<KafkaServerSettings> kafkaSettings,
    ILogger<BookingConfirmationFailedPaymentConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<BookingConfirmationFailedIntegrationEvent>(
        KafkaTopics.BookingConfirmationFailed, $"payment-service-{KafkaTopics.BookingConfirmationFailed}", kafkaSettings, logger)
{
    protected override async Task ProcessAsync(BookingConfirmationFailedIntegrationEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new RefundPaymentCommand(message.BookingId, message.Reason), cancellationToken);
    }
}
