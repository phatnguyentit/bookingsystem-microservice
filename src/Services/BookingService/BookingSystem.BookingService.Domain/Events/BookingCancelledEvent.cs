using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Domain.Events;

public record BookingCancelledEvent(
    BookingId BookingId,
    UserId UserId,
    CatalogId CatalogId,
    string Reason) : IDomainEvent
{
    public static BookingCancelledEvent Create(
        BookingId bookingId, UserId userId, CatalogId catalogId, string reason)
        => new(bookingId, userId, catalogId, reason);
}
