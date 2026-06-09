namespace BookingSystem.Shared.Contracts.DTOs;

public record CatalogDto(
    Guid Id,
    string Title,
    string Description,
    decimal PricePerNight,
    string Currency,
    bool IsAvailable);

public static class ListingDtoExtensions
{
    public static bool IsAvailable(this CatalogDto listing, DateRange period) =>
        listing.IsAvailable;

    public static (decimal Amount, string Currency) CalculatePrice(this CatalogDto listing, DateRange period) =>
        (listing.PricePerNight * period.Nights, listing.Currency);
}

public record DateRange(DateOnly CheckIn, DateOnly CheckOut)
{
    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;
}
