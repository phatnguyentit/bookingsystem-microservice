using BookingSystem.BookingService.Domain.Exceptions;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;

namespace BookingService.Domain.Tests.ValueObjects;

public class DateRangeTests
{
    [Fact]
    public void Constructor_CheckOutAfterCheckIn_SetsProperties()
    {
        var range = new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5));

        range.CheckIn.Should().Be(new DateOnly(2026, 8, 1));
        range.CheckOut.Should().Be(new DateOnly(2026, 8, 5));
    }

    [Fact]
    public void Constructor_CheckOutEqualsCheckIn_ThrowsBookingDomainException()
    {
        var date = new DateOnly(2026, 8, 1);

        var act = () => new DateRange(date, date);

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Constructor_CheckOutBeforeCheckIn_ThrowsBookingDomainException()
    {
        var act = () => new DateRange(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1));

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Nights_ReturnsNumberOfNightsBetweenDates()
    {
        var range = new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5));

        range.Nights.Should().Be(4);
    }
}
