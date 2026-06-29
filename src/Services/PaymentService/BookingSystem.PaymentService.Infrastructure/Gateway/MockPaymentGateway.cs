using Microsoft.Extensions.Logging;

namespace BookingSystem.PaymentService.Infrastructure.Gateway;

/// <summary>
/// Stand-in gateway that always approves the charge. Swap this for a real HTTP-backed
/// implementation; the handler already treats any failure — a declined result or a thrown
/// exception (timeout, network error) — as a failed payment.
/// </summary>
public class MockPaymentGateway(ILogger<MockPaymentGateway> logger) : IPaymentGateway
{
    public Task<PaymentGatewayResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mock gateway charging {Amount} {Currency} for booking {BookingId} (idempotency key {Key})",
            request.Amount, request.Currency, request.BookingId, request.IdempotencyKey);

        return Task.FromResult(PaymentGatewayResult.Approved($"mock-{request.IdempotencyKey}"));
    }

    public Task<PaymentGatewayResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mock gateway refunding {Amount} {Currency} for booking {BookingId} (idempotency key {Key}); reason: {Reason}",
            request.Amount, request.Currency, request.BookingId, request.IdempotencyKey, request.Reason);

        return Task.FromResult(PaymentGatewayResult.Approved($"refund-{request.IdempotencyKey}"));
    }
}
