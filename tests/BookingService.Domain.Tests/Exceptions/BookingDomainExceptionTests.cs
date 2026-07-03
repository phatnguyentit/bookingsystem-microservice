using BookingSystem.BookingService.Domain.Exceptions;
using FluentAssertions;

namespace BookingService.Domain.Tests.Exceptions;

public class BookingDomainExceptionTests
{
    [Fact]
    public void Constructor_PreservesMessage()
    {
        var exception = new BookingDomainException("Only pending bookings can be confirmed.");

        exception.Message.Should().Be("Only pending bookings can be confirmed.");
    }
}
