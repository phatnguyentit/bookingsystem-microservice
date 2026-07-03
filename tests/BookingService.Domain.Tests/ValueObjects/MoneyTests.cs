using BookingSystem.BookingService.Domain.Exceptions;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;

namespace BookingService.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var result = new Money(100m, "USD").Add(new Money(50.5m, "USD"));

        result.Should().Be(new Money(150.5m, "USD"));
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsBookingDomainException()
    {
        var act = () => new Money(100m, "USD").Add(new Money(50m, "EUR"));

        act.Should().Throw<BookingDomainException>();
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        new Money(100m, "USD").Should().Be(new Money(100m, "USD"));
    }
}
