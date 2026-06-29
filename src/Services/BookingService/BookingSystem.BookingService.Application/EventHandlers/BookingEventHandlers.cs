using BookingSystem.BookingService.Domain.Events;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using MediatR;

namespace BookingSystem.BookingService.Application.EventHandlers;

public class PublishBookingCreatedHandler(IEventPublisher publisher) : INotificationHandler<BookingCreatedEvent>
{
    public Task Handle(BookingCreatedEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(KafkaTopics.BookingCreated, new BookingCreatedIntegrationEvent(
                notification.BookingId.Value,
                notification.UserId.Value,
                notification.CatalogId.Value,
                notification.Period.CheckIn,
                notification.Period.CheckOut,
                notification.TotalPrice.Amount,
                notification.TotalPrice.Currency,
                DateTime.UtcNow), cancellationToken);
}

public class PublishBookingCancelledHandler(IEventPublisher publisher) : INotificationHandler<BookingCancelledEvent>
{
    public Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(KafkaTopics.BookingCancelled,
            new BookingCancelledIntegrationEvent(
                notification.BookingId.Value,
                notification.UserId.Value,
                notification.CatalogId.Value,
                notification.Reason,
                DateTime.UtcNow), cancellationToken);
}

public class PublishBookingConfirmationFailedHandler(IEventPublisher publisher) : INotificationHandler<BookingConfirmationFailedEvent>
{
    public Task Handle(BookingConfirmationFailedEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync(KafkaTopics.BookingConfirmationFailed,
            new BookingConfirmationFailedIntegrationEvent(
                notification.BookingId.Value,
                notification.UserId.Value,
                notification.Reason,
                DateTime.UtcNow), cancellationToken);
}
