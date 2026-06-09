using BookingSystem.BookingService.Domain.Events;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using MediatR;

namespace BookingSystem.BookingService.Application.EventHandlers;

public class PublishBookingCreatedHandler(IEventPublisher publisher)
    : INotificationHandler<BookingCreatedEvent>
{
    public Task Handle(BookingCreatedEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync("booking.created",
            new BookingCreatedIntegrationEvent(
                notification.BookingId.Value,
                notification.UserId.Value,
                notification.CatalogId.Value,
                default, // populated in infrastructure via full booking lookup
                default,
                0,
                string.Empty,
                DateTime.UtcNow), cancellationToken);
}

public class PublishBookingCancelledHandler(IEventPublisher publisher)
    : INotificationHandler<BookingCancelledEvent>
{
    public Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
        => publisher.PublishAsync("booking.cancelled",
            new BookingCancelledIntegrationEvent(
                notification.BookingId.Value,
                Guid.Empty,
                Guid.Empty,
                notification.Reason,
                DateTime.UtcNow), cancellationToken);
}
