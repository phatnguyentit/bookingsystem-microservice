using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingService.Application.Tests;

/// <summary>Builds Booking aggregates in a known state, with domain events cleared.</summary>
internal static class TestBooking
{
    public static Booking Pending(
        Guid? userId = null,
        Guid? catalogId = null,
        DateOnly? checkIn = null,
        DateOnly? checkOut = null,
        decimal amount = 400m,
        string currency = "USD")
    {
        var booking = Booking.Create(
            new UserId(userId ?? Guid.NewGuid()),
            new CatalogId(catalogId ?? Guid.NewGuid()),
            new DateRange(checkIn ?? new DateOnly(2026, 8, 1), checkOut ?? new DateOnly(2026, 8, 5)),
            new Money(amount, currency));
        booking.ClearDomainEvents();
        return booking;
    }

    public static Booking Confirmed()
    {
        var booking = Pending();
        booking.Confirm();
        booking.ClearDomainEvents();
        return booking;
    }

    public static Booking Cancelled()
    {
        var booking = Pending();
        booking.Cancel("cancelled for test setup");
        booking.ClearDomainEvents();
        return booking;
    }

    public static Booking Completed()
    {
        var booking = Confirmed();
        booking.Complete();
        booking.ClearDomainEvents();
        return booking;
    }
}
