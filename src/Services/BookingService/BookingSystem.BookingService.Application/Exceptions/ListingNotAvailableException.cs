using BookingSystem.BookingService.Domain.ValueObjects;

namespace BookingSystem.BookingService.Application.Exceptions;

public class ListingNotAvailableException(Guid catalogId, DateRange period)
    : Exception($"Listing {catalogId} is not available from {period.CheckIn} to {period.CheckOut}.");
