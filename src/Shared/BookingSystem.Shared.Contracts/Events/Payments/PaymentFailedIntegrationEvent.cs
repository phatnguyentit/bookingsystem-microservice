namespace BookingSystem.Shared.Contracts.Events.Payments;

public record PaymentFailedIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    string Reason,
    DateTimeOffset OccurredAt);
