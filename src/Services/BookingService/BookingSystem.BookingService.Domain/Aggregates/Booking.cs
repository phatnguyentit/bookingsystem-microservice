using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.Exceptions;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Domain;

public class Booking : AggregateRoot<BookingId>
{
    public UserId UserId { get; private set; } = default!;
    public CatalogId CatalogId { get; private set; } = default!;
    public DateRange Period { get; private set; } = default!;
    public Money TotalPrice { get; private set; } = default!;
    public BookingStatus Status { get; private set; }

    private Booking() { } // EF constructor

    public static Booking Create(
        UserId userId,
        CatalogId catalogId,
        DateRange period,
        Money totalPrice)
    {
        var booking = new Booking
        {
            Id = BookingId.New(),
            UserId = userId,
            CatalogId = catalogId,
            Period = period,
            TotalPrice = totalPrice,
            Status = BookingStatus.Pending
        };
        booking.AddDomainEvent(BookingCreatedEvent.Create(booking.Id, userId, catalogId, period, totalPrice));
        return booking;
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new BookingDomainException("Only pending bookings can be confirmed.");
        Status = BookingStatus.Confirmed;
        AddDomainEvent(BookingConfirmedEvent.Create(Id));
    }

    // Records that a captured payment could not be confirmed against this booking. Intentionally
    // does not change Status — the booking stays in its terminal state; this only raises a
    // compensation event so a downstream refund/cleanup can be choreographed via the outbox.
    public void RejectConfirmation(string reason)
        => AddDomainEvent(BookingConfirmationFailedEvent.Create(Id, UserId, reason));

    public void Cancel(string reason)
    {
        if (Status == BookingStatus.Cancelled)
            throw new BookingDomainException("Booking is already cancelled.");
        Status = BookingStatus.Cancelled;
        AddDomainEvent(BookingCancelledEvent.Create(Id, UserId, CatalogId, reason));
    }

    public void Complete()
    {
        if (Status != BookingStatus.Confirmed)
            throw new BookingDomainException("Only confirmed bookings can be completed.");
        Status = BookingStatus.Completed;
    }
}
