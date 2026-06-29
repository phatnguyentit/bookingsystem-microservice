namespace BookingSystem.Shared.Contracts.Events.Bookings;

public record BookingConfirmationFailedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    string Reason,
    DateTimeOffset OccurredAt);
