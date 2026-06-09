using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Domain.Events;

public record BookingCreatedEvent(
    BookingId BookingId,
    UserId UserId,
    CatalogId CatalogId,
    DateRange Period,
    Money TotalPrice) : IDomainEvent
{
    public static BookingCreatedEvent Create(
        BookingId bookingId, UserId userId, CatalogId catalogId,
        DateRange period, Money totalPrice)
        => new(bookingId, userId, catalogId, period, totalPrice);
}