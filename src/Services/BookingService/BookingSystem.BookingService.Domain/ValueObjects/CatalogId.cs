namespace BookingSystem.BookingService.Domain.ValueObjects;

public record CatalogId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
