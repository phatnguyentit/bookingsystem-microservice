using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Domain.Events;

public record BookingCreatedEvent(
    BookingId BookingId,
    UserId UserId,
    CatalogId CatalogId) : IDomainEvent
{
    public static BookingCreatedEvent Create(BookingId bookingId, UserId userId, CatalogId catalogId) => new(bookingId, userId, catalogId);
}