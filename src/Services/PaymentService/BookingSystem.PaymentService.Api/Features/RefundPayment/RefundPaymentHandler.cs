using BookingSystem.PaymentService.Infrastructure.Persistence;
using MediatR;

namespace BookingSystem.PaymentService.Api.Features.RefundPayment;

public class RefundPaymentHandler(IPaymentRepository repository, ILogger<RefundPaymentHandler> logger)
    : IRequestHandler<RefundPaymentCommand>
{
    public async Task Handle(RefundPaymentCommand cmd, CancellationToken cancellationToken)
    {
        var payment = await repository.GetCapturedByBookingIdAsync(cmd.BookingId, cancellationToken);

        // No captured payment for this booking — nothing to reverse. This is unexpected (the
        // compensation event only fires after a payment.succeeded), so log it rather than fail:
        // retrying could never make a payment appear.
        if (payment is null)
        {
            logger.LogWarning(
                "Refund requested for booking {BookingId} but no captured payment was found; nothing to refund. Reason: {Reason}",
                cmd.BookingId, cmd.Reason);
            return;
        }

        // Idempotent: booking.confirmation.failed is at-least-once. If the refund is already in flight
        // (RefundPending), done (Refunded), or escalated for manual handling (RefundFailed), a
        // redelivery is a no-op — we must not reset a RefundFailed payment back to RefundPending.
        if (payment.Status is PaymentStatus.RefundPending or PaymentStatus.Refunded or PaymentStatus.RefundFailed)
        {
            logger.LogInformation(
                "Payment {PaymentId} for booking {BookingId} already has refund status {Status}; ignoring duplicate compensation event.",
                payment.Id, cmd.BookingId, payment.Status);
            return;
        }

        // Record the refund *obligation* durably and stop. The actual gateway reversal is driven to
        // completion by RefundProcessor, which retries transient failures indefinitely — so the debt
        // can never be dropped. This consumer step is a single DB write that cannot fail in a way
        // that would warrant a dead-letter (which is why a refund never goes to the DLQ).
        payment.Status = PaymentStatus.RefundPending;
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Recorded refund obligation for payment {PaymentId} (booking {BookingId}). Reason: {Reason}",
            payment.Id, cmd.BookingId, cmd.Reason);
    }
}
