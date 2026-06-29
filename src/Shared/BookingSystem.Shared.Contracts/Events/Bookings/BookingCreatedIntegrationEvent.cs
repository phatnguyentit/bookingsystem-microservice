namespace BookingSystem.Shared.Contracts.Events.Bookings;

public record BookingCreatedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid CatalogId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt);
