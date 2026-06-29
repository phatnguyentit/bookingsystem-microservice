namespace BookingSystem.Shared.Contracts.Events;

public record BookingConfirmationFailedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    string Reason,
    DateTime OccurredAt);
