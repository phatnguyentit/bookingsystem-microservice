using BookingSystem.BookingService.Application.Commands.CancelBooking;
using BookingSystem.BookingService.Application.Exceptions;
using BookingSystem.BookingService.Application.Interfaces.UoW;
using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace BookingService.Application.Tests.Commands;

public class CancelBookingHandlerTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CancelBookingHandler CreateHandler() => new(_bookingRepo, _unitOfWork);

    private Booking SetupBooking(Booking booking)
    {
        _bookingRepo.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        return booking;
    }

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns((Booking?)null);

        var act = () => CreateHandler().Handle(new CancelBookingCommand(Guid.NewGuid(), "reason"), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingBooking_CancelsAndCommits()
    {
        var booking = SetupBooking(TestBooking.Pending());

        await CreateHandler().Handle(new CancelBookingCommand(booking.Id.Value, "changed my mind"), default);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCancelledEvent);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConfirmedBooking_CancelsAndCommits()
    {
        var booking = SetupBooking(TestBooking.Confirmed());

        await CreateHandler().Handle(new CancelBookingCommand(booking.Id.Value, "payment reversed"), default);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCancelledBooking_IsIdempotentAndDoesNotCommit()
    {
        var booking = SetupBooking(TestBooking.Cancelled());

        var act = () => CreateHandler().Handle(new CancelBookingCommand(booking.Id.Value, "redelivered event"), default);

        await act.Should().NotThrowAsync();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.DomainEvents.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
