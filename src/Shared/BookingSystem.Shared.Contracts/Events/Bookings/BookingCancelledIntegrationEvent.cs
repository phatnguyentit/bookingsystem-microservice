namespace BookingSystem.Shared.Contracts.Events.Bookings;

public record BookingCancelledIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid CatalogId,
    string Reason,
    DateTimeOffset OccurredAt);
