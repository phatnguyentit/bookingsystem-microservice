namespace BookingSystem.PaymentService.Infrastructure.Gateway;

/// <summary>
/// Seam for the external payment gateway. The real implementation would call a third-party
/// API (Stripe, Adyen, …) over HTTP; <see cref="MockPaymentGateway"/> stands in for now.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reverses a previously captured charge. Used by the saga compensation (refund) path.</summary>
    Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default);
}

/// <param name="IdempotencyKey">
/// Stable key (the PaymentId) the gateway uses to de-duplicate retried charges, so replaying
/// the same attempt after a crash never double-charges the customer.
/// </param>
public record PaymentChargeRequest(
    string IdempotencyKey,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

/// <param name="IdempotencyKey">
/// Stable key (the PaymentId) the gateway uses to de-duplicate retried refunds, so replaying
/// the same compensation after a crash never refunds twice.
/// </param>
public record PaymentRefundRequest(
    string IdempotencyKey,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Reason);

public record PaymentGatewayResult(bool Success, string? Reference, string? FailureReason)
{
    public static PaymentGatewayResult Approved(string reference) => new(true, reference, null);
    public static PaymentGatewayResult Declined(string reason) => new(false, null, reason);
}

/// <summary>Thrown when the gateway declines a charge; the message carries the decline reason.</summary>
public class PaymentGatewayException(string message) : Exception(message);
