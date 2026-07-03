using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.Exceptions;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;

namespace BookingService.Domain.Tests;

public class BookingTests
{
    private static Booking CreatePendingBooking() =>
        Booking.Create(
            new UserId(Guid.NewGuid()),
            new CatalogId(Guid.NewGuid()),
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)),
            new Money(400m, "USD"));

    private static Booking CreateConfirmedBooking()
    {
        var booking = CreatePendingBooking();
        booking.Confirm();
        return booking;
    }

    [Fact]
    public void Create_ValidInput_SetsStatusToPending()
    {
        var booking = CreatePendingBooking();

        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Create_ValidInput_RaisesBookingCreatedEvent()
    {
        var booking = CreatePendingBooking();

        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingCreatedEvent>();
    }

    [Fact]
    public void Confirm_WhenPending_SetsStatusToConfirmedAndRaisesEvent()
    {
        var booking = CreatePendingBooking();

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmedEvent);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_ThrowsBookingDomainException()
    {
        var booking = CreateConfirmedBooking();

        var act = () => booking.Confirm();

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Confirm_WhenCancelled_ThrowsBookingDomainException()
    {
        var booking = CreatePendingBooking();
        booking.Cancel("changed my mind");

        var act = () => booking.Confirm();

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Cancel_WhenPending_SetsStatusToCancelledAndRaisesEvent()
    {
        var booking = CreatePendingBooking();

        booking.Cancel("changed my mind");

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCancelledEvent);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsBookingDomainException()
    {
        var booking = CreatePendingBooking();
        booking.Cancel("first reason");

        var act = () => booking.Cancel("second reason");

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Complete_WhenConfirmed_SetsStatusToCompleted()
    {
        var booking = CreateConfirmedBooking();

        booking.Complete();

        booking.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public void Complete_WhenPending_ThrowsBookingDomainException()
    {
        var booking = CreatePendingBooking();

        var act = () => booking.Complete();

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void RejectConfirmation_DoesNotChangeStatus_AndRaisesFailureEvent()
    {
        var booking = CreatePendingBooking();
        booking.Cancel("cancelled before payment settled");
        booking.ClearDomainEvents();

        booking.RejectConfirmation("booking already cancelled");

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingConfirmationFailedEvent>();
    }
}
