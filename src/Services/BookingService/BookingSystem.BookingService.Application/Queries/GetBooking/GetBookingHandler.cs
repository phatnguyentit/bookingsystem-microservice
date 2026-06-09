using BookingSystem.BookingService.Application.DTOs;
using BookingSystem.BookingService.Application.Exceptions;
using BookingSystem.BookingService.Domain.Repositories;
using BookingSystem.BookingService.Domain.ValueObjects;
using MediatR;

namespace BookingSystem.BookingService.Application.Queries.GetBooking;

public class GetBookingHandler(IBookingRepository repo)
    : IRequestHandler<GetBookingQuery, BookingDto>
{
    public async Task<BookingDto> Handle(GetBookingQuery q, CancellationToken cancellationToken)
    {
        var booking = await repo.GetByIdAsync(new BookingId(q.BookingId), cancellationToken)
            ?? throw new NotFoundException($"Booking {q.BookingId} not found.");

        return new BookingDto(
            booking.Id.Value,
            booking.UserId.Value,
            booking.CatalogId.Value,
            booking.Period.CheckIn,
            booking.Period.CheckOut,
            booking.TotalPrice.Amount,
            booking.TotalPrice.Currency,
            booking.Status.ToString());
    }
}
