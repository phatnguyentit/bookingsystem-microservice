namespace BookingSystem.Shared.Contracts.Events.Payments;

/// <summary>
/// A captured payment could not be refunded automatically (the gateway permanently declined).
/// Signals that a manual refund / reconciliation is owed to the customer.
/// </summary>
public record PaymentRefundFailedIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTimeOffset OccurredAt);
