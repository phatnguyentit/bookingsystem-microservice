using BookingSystem.PaymentService.Infrastructure.Gateway;
using BookingSystem.PaymentService.Infrastructure.Outbox;
using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Contracts.Events.Payments;
using MediatR;

namespace BookingSystem.PaymentService.Api.Features.ProcessPayment;

public class ProcessPaymentHandler(IPaymentRepository repo, IPaymentGateway gateway, ILogger<ProcessPaymentHandler> logger)
: IRequestHandler<ProcessPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ProcessPaymentCommand cmd, CancellationToken cancellationToken)
    {
        // Initial state
        var payment = new Payment
        {
            Id = PaymentId.New(),
            BookingId = cmd.BookingId,
            UserId = cmd.UserId,
            Amount = cmd.Amount,
            Currency = cmd.Currency,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // PaymentId is used as idempotency key to avoid duplicate charges in case of retries. So, save it to the database before calling the payment gateway
        await repo.AddAsync(payment, cancellationToken);

        try
        {
            // Call payment gateway to charge the user with idempotency key to avoid duplicate charges in case of retries. If the gateway call fails (e.g. network issue) or declines the payment, an exception is thrown and we mark the payment as Failed in the catch block below.
            var result = await gateway.ChargeAsync(new PaymentChargeRequest(
                IdempotencyKey: payment.Id.Value.ToString(),
                payment.BookingId,
                payment.UserId,
                payment.Amount,
                payment.Currency,
                cmd.PaymentMethod), cancellationToken);

            // TODO: Double check if we can use WebHook to avoid waiting for the gateway response, which would make the process more resilient to transient gateway issues. The flow would be: mark Pending + stage event → emit → gateway calls back with success/failure → update status + stage outcome event. The main challenge is ensuring the callback is reliably received and processed (e.g. via retry policies, dead-letter queue, reconciliation job).
            if (!result.Success)
                throw new PaymentGatewayException(result.FailureReason ?? "Payment was declined.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Payment {PaymentId} failed for booking {BookingId}", payment.Id, payment.BookingId);
            await MarkFailedAsync(payment, ex.Message, cancellationToken);
            throw;
        }

        await MarkSucceededAsync(payment, cancellationToken);

        return payment.Id.Value;
    }

    private async Task MarkSucceededAsync(Payment payment, CancellationToken cancellationToken)
    {
        payment.Status = PaymentStatus.Succeeded;
        repo.AddOutboxMessage(OutboxMessage.For(KafkaTopics.PaymentSucceeded, new PaymentSucceededIntegrationEvent(
                payment.Id.Value, payment.BookingId, payment.UserId,
                payment.Amount, payment.Currency, DateTimeOffset.UtcNow)));
                
        await repo.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(Payment payment, string reason, CancellationToken cancellationToken)
    {
        payment.Status = PaymentStatus.Failed;
        repo.AddOutboxMessage(OutboxMessage.For(KafkaTopics.PaymentFailed, new PaymentFailedIntegrationEvent(
                payment.Id.Value, payment.BookingId, payment.UserId,
                reason, DateTimeOffset.UtcNow)));

        await repo.SaveChangesAsync(cancellationToken);
    }
}
