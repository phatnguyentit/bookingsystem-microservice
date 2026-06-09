using BookingSystem.PaymentService.Api.Features.ProcessPayment;
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
        "booking.created", "payment-service-booking.created", kafkaSettings, logger)
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
