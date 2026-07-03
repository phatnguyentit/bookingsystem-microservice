namespace BookingSystem.Shared.Contracts.Events.Payments;

public record PaymentSucceededIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt);
