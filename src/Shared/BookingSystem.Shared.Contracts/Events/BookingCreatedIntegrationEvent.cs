namespace BookingSystem.Shared.Contracts.Events;

public record BookingCreatedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid CatalogId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal Amount,
    string Currency,
    DateTime OccurredAt);
