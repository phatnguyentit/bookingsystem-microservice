namespace BookingSystem.Shared.Contracts.Events.Payments;

public record PaymentRefundedIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTimeOffset OccurredAt);
