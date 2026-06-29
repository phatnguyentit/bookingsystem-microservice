using BookingSystem.PaymentService.Infrastructure.Gateway;
using BookingSystem.PaymentService.Infrastructure.Outbox;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.PaymentService.Api.Refunds;

/// <summary>
/// Drives durable refund obligations to completion. Polls payments left in
/// <see cref="PaymentStatus.RefundPending"/> by <c>RefundPaymentHandler</c> and reverses each one
/// at the gateway. A refund is a financial debt, not a discardable message: a transient gateway
/// failure leaves the payment <c>RefundPending</c> so it is retried on the next cycle indefinitely —
/// the obligation is never dead-lettered or dropped. On success the payment becomes
/// <see cref="PaymentStatus.Refunded"/> and a <c>payment.refunded</c> event is staged via the outbox.
/// </summary>
public class RefundProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<RefundProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private const string RefundReason = "Booking confirmation failed after payment capture.";

    // Number of consecutive transient failures for one payment before we page an operator. The
    // refund still keeps retrying — we never stop chasing the debt — but a human is alerted that
    // the gateway may be down. Tracked in-memory (resets on restart, which is acceptable: a
    // persistently failing refund will re-accumulate and re-alert).
    private const int TransientAlertThreshold = 5;
    private readonly Dictionary<Guid, int> _transientAttempts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRefundsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error in RefundProcessor polling loop");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingRefundsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<IPaymentGateway>();

        var pending = await db.Payments
            .Where(p => p.Status == PaymentStatus.RefundPending)
            .OrderBy(p => p.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        foreach (var payment in pending)
        {
            try
            {
                // PaymentId is the idempotency key, so a replayed refund never reverses twice.
                var result = await gateway.RefundAsync(new PaymentRefundRequest(
                    IdempotencyKey: payment.Id.Value.ToString(),
                    payment.BookingId,
                    payment.Amount,
                    payment.Currency,
                    RefundReason), ct);

                if (!result.Success)
                {
                    // A declined refund is a *permanent* business failure — automated retry can never
                    // succeed. Move to the RefundFailed terminal state, emit a compensation-failed
                    // event, and page an operator: a manual refund is owed to the customer.
                    payment.Status = PaymentStatus.RefundFailed;
                    db.OutboxMessages.Add(OutboxMessage.For(KafkaTopics.PaymentRefundFailed, new PaymentRefundFailedIntegrationEvent(
                        payment.Id.Value, payment.BookingId, payment.UserId,
                        payment.Amount, payment.Currency,
                        result.FailureReason ?? "Refund was declined.", DateTime.UtcNow)));
                    _transientAttempts.Remove(payment.Id.Value);

                    // TODO(metrics): emit an OpenTelemetry counter (payments.refund.failed) here so
                    // dashboards/alerts can fire on the rate, not just on the log. Tracked separately.
                    logger.LogCritical(
                        "MANUAL ACTION REQUIRED: refund permanently declined for payment {PaymentId} (booking {BookingId}, {Amount} {Currency}): {Reason}. Marked RefundFailed; issue a manual refund.",
                        payment.Id, payment.BookingId, payment.Amount, payment.Currency, result.FailureReason);
                    continue;
                }

                payment.Status = PaymentStatus.Refunded;
                db.OutboxMessages.Add(OutboxMessage.For(KafkaTopics.PaymentRefunded, new PaymentRefundedIntegrationEvent(
                    payment.Id.Value, payment.BookingId, payment.UserId,
                    payment.Amount, payment.Currency, RefundReason, DateTime.UtcNow)));
                _transientAttempts.Remove(payment.Id.Value);

                logger.LogInformation(
                    "Refunded payment {PaymentId} ({Amount} {Currency}) for booking {BookingId}",
                    payment.Id, payment.Amount, payment.Currency, payment.BookingId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Transient gateway/network failure: leave the payment RefundPending and retry next
                // cycle. The debt is durable in the DB, so it survives restarts and broker outages —
                // we never stop chasing it. Page an operator once if it stays stuck past the threshold.
                var attempts = _transientAttempts[payment.Id.Value] =
                    _transientAttempts.GetValueOrDefault(payment.Id.Value) + 1;

                if (attempts == TransientAlertThreshold)
                {
                    // TODO(metrics): emit an OpenTelemetry counter (payments.refund.stuck) here too.
                    logger.LogCritical(ex,
                        "MANUAL ATTENTION: refund for payment {PaymentId} (booking {BookingId}) has failed {Attempts} consecutive times; still retrying. Check gateway connectivity.",
                        payment.Id, payment.BookingId, attempts);
                }
                else if (attempts < TransientAlertThreshold)
                {
                    logger.LogWarning(ex,
                        "Refund attempt {Attempts} failed for payment {PaymentId} (booking {BookingId}); will retry next cycle",
                        attempts, payment.Id, payment.BookingId);
                }
                // attempts > threshold: already paged — keep retrying silently to avoid log spam.
            }
        }

        // Persist all status changes + staged outbox rows for this batch in one transaction.
        await db.SaveChangesAsync(ct);
    }
}
