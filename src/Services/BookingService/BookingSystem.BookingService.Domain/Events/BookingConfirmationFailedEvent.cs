using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Domain.Events;

// Raised when a payment was captured but the booking can no longer be confirmed
// (e.g. it was already cancelled). Carries the compensation signal for a refund/cleanup.
public record BookingConfirmationFailedEvent(
    BookingId BookingId,
    UserId UserId,
    string Reason) : IDomainEvent
{
    public static BookingConfirmationFailedEvent Create(
        BookingId bookingId, UserId userId, string reason)
        => new(bookingId, userId, reason);
}
