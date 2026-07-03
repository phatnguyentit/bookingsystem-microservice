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

        // Saga conflict: payment was captured but the booking is in a terminal state (e.g. it was
        // already Cancelled by the payment.failed path). Confirming is impossible, so instead of
        // throwing (which the consumer would treat as transient and retry) we emit a compensation
        // signal via the outbox so a refund/cleanup can be choreographed downstream.
        if (booking.Status != BookingStatus.Pending) // Cancelled, possibly
        {
            booking.RejectConfirmation(
                $"Payment succeeded but booking is {booking.Status} and cannot be confirmed.");
            await unitOfWork.CommitAsync(cancellationToken);
            return;
        }

        booking.Confirm();
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
