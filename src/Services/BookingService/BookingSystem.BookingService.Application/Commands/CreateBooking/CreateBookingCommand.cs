using BookingSystem.BookingService.Domain.ValueObjects;
using MediatR;

namespace BookingSystem.BookingService.Application.Commands.CreateBooking;

public record CreateBookingCommand(
    Guid UserId,
    Guid CatalogId,
    DateOnly CheckIn,
    DateOnly CheckOut) : IRequest<BookingId>;
