namespace BookingSystem.PaymentService.Infrastructure.Persistence;

/// <summary>
/// Lifecycle states for a <see cref="Payment"/>, stored as the <see cref="Payment.Status"/>
/// string column. A payment starts <see cref="Pending"/> the moment the attempt is recorded
/// and only becomes <see cref="Succeeded"/> or <see cref="Failed"/> after the gateway responds.
/// </summary>
public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";

    /// <summary>
    /// A captured payment owed back to the customer because the booking could not be confirmed.
    /// The obligation is durable; <c>RefundProcessor</c> drives it to <see cref="Refunded"/>,
    /// retrying transient gateway failures indefinitely so the debt is never dropped.
    /// </summary>
    public const string RefundPending = "RefundPending";

    /// <summary>A captured payment that was successfully reversed at the gateway (saga compensation).</summary>
    public const string Refunded = "Refunded";

    /// <summary>
    /// A refund the gateway permanently declined (e.g. refund window closed). The automated path
    /// cannot resolve this — a human must issue a manual refund. Entering this state raises an
    /// operator alert and emits <c>payment.refund.failed</c>.
    /// </summary>
    public const string RefundFailed = "RefundFailed";
}
