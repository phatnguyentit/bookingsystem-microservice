namespace BookingSystem.Shared.Contracts.Events;

public record BookingCancelledIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid CatalogId,
    string Reason,
    DateTime OccurredAt);
