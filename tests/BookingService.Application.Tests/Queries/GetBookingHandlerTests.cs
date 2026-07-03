using BookingSystem.BookingService.Application.Exceptions;
using BookingSystem.BookingService.Application.Queries.GetBooking;
using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace BookingService.Application.Tests.Queries;

public class GetBookingHandlerTests
{
    private readonly IBookingRepository _repo = Substitute.For<IBookingRepository>();

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns((Booking?)null);

        var act = () => new GetBookingHandler(_repo).Handle(new GetBookingQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_BookingExists_MapsAllFieldsToDto()
    {
        var userId = Guid.NewGuid();
        var catalogId = Guid.NewGuid();
        var booking = TestBooking.Pending(
            userId: userId,
            catalogId: catalogId,
            checkIn: new DateOnly(2026, 8, 1),
            checkOut: new DateOnly(2026, 8, 5),
            amount: 400m,
            currency: "USD");
        _repo.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var dto = await new GetBookingHandler(_repo).Handle(new GetBookingQuery(booking.Id.Value), default);

        dto.Id.Should().Be(booking.Id.Value);
        dto.UserId.Should().Be(userId);
        dto.CatalogId.Should().Be(catalogId);
        dto.CheckIn.Should().Be(new DateOnly(2026, 8, 1));
        dto.CheckOut.Should().Be(new DateOnly(2026, 8, 5));
        dto.Amount.Should().Be(400m);
        dto.Currency.Should().Be("USD");
        dto.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_CancelledBooking_MapsStatusAsString()
    {
        var booking = TestBooking.Cancelled();
        _repo.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var dto = await new GetBookingHandler(_repo).Handle(new GetBookingQuery(booking.Id.Value), default);

        dto.Status.Should().Be("Cancelled");
    }
}
