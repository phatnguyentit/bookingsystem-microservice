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
}
