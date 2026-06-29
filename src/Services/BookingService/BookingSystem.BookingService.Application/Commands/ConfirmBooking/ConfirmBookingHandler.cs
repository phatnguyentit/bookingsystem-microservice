using BookingSystem.BookingService.Application.Exceptions;
using BookingSystem.BookingService.Application.Interfaces.UoW;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Domain.ValueObjects;
using MediatR;

namespace BookingSystem.BookingService.Application.Commands.ConfirmBooking;

public class ConfirmBookingHandler(
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmBookingCommand>
{
    public async Task Handle(ConfirmBookingCommand cmd, CancellationToken cancellationToken)
    {
        var booking = await bookingRepo.GetByIdAsync(new BookingId(cmd.BookingId), cancellationToken)
            ?? throw new NotFoundException($"Booking {cmd.BookingId} not found.");

        // Idempotent: payment.succeeded is at-least-once, so a redelivery on an already-confirmed
        // booking is the desired end state — treat it as success rather than throwing.
        if (booking.Status == BookingStatus.Confirmed)
            return;

        booking.Confirm();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
