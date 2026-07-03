using BookingSystem.BookingService.Application.Commands.ConfirmBooking;
using BookingSystem.BookingService.Application.Exceptions;
using BookingSystem.BookingService.Application.Interfaces.UoW;
using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Domain.Events;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace BookingService.Application.Tests.Commands;

public class ConfirmBookingHandlerTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ConfirmBookingHandler CreateHandler() => new(_bookingRepo, _unitOfWork);

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

        var act = () => CreateHandler().Handle(new ConfirmBookingCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingBooking_ConfirmsAndCommits()
    {
        var booking = SetupBooking(TestBooking.Pending());

        await CreateHandler().Handle(new ConfirmBookingCommand(booking.Id.Value), default);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmedEvent);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyConfirmedBooking_IsIdempotentAndDoesNotCommit()
    {
        var booking = SetupBooking(TestBooking.Confirmed());

        var act = () => CreateHandler().Handle(new ConfirmBookingCommand(booking.Id.Value), default);

        await act.Should().NotThrowAsync();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.DomainEvents.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CancelledBooking_RejectsConfirmationWithoutChangingStatus()
    {
        var booking = SetupBooking(TestBooking.Cancelled());

        await CreateHandler().Handle(new ConfirmBookingCommand(booking.Id.Value), default);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BookingConfirmationFailedEvent>();
        // The compensation event must be committed so the outbox publishes it
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CompletedBooking_RejectsConfirmationWithoutChangingStatus()
    {
        var booking = SetupBooking(TestBooking.Completed());

        await CreateHandler().Handle(new ConfirmBookingCommand(booking.Id.Value), default);

        booking.Status.Should().Be(BookingStatus.Completed);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmationFailedEvent);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
