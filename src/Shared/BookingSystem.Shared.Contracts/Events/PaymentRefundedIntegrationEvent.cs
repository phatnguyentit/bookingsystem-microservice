namespace BookingSystem.Shared.Contracts.Events;

public record PaymentRefundedIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTime OccurredAt);
