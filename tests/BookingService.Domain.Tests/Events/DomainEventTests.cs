using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;

namespace BookingService.Domain.Tests.Events;

public class BookingCreatedEventTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var bookingId = BookingId.New();
        var userId = new UserId(Guid.NewGuid());
        var catalogId = new CatalogId(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5));
        var price = new Money(400m, "USD");

        var @event = BookingCreatedEvent.Create(bookingId, userId, catalogId, period, price);

        @event.Should().Be(new BookingCreatedEvent(bookingId, userId, catalogId, period, price));
        @event.Should().BeAssignableTo<IDomainEvent>();
    }
}

public class BookingConfirmedEventTests
{
    [Fact]
    public void Create_SetsBookingId()
    {
        var bookingId = BookingId.New();

        var @event = BookingConfirmedEvent.Create(bookingId);

        @event.BookingId.Should().Be(bookingId);
        @event.Should().BeAssignableTo<IDomainEvent>();
    }
}

public class BookingCancelledEventTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var bookingId = BookingId.New();
        var userId = new UserId(Guid.NewGuid());
        var catalogId = new CatalogId(Guid.NewGuid());

        var @event = BookingCancelledEvent.Create(bookingId, userId, catalogId, "changed my mind");

        @event.Should().Be(new BookingCancelledEvent(bookingId, userId, catalogId, "changed my mind"));
        @event.Should().BeAssignableTo<IDomainEvent>();
    }
}

public class BookingConfirmationFailedEventTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var bookingId = BookingId.New();
        var userId = new UserId(Guid.NewGuid());

        var @event = BookingConfirmationFailedEvent.Create(bookingId, userId, "booking already cancelled");

        @event.Should().Be(new BookingConfirmationFailedEvent(bookingId, userId, "booking already cancelled"));
        @event.Should().BeAssignableTo<IDomainEvent>();
    }
}
