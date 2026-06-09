using BookingSystem.PaymentService.Infrastructure.Persistence;
using BookingSystem.Shared.Contracts.Events;
using BookingSystem.Shared.Messaging;
using MediatR;

namespace BookingSystem.PaymentService.Api.Features.ProcessPayment;

public record ProcessPaymentCommand(
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string PaymentMethod) : IRequest<Guid>;

public class ProcessPaymentHandler(
    IPaymentRepository repo,
    IEventPublisher publisher) : IRequestHandler<ProcessPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ProcessPaymentCommand cmd, CancellationToken cancellationToken)
    {
        var paymentId = PaymentId.New();

        try
        {
            // Simplified: real implementation would call a payment gateway here
            var payment = new Payment
            {
                Id = paymentId,
                BookingId = cmd.BookingId,
                UserId = cmd.UserId,
                Amount = cmd.Amount,
                Currency = cmd.Currency,
                Status = "Succeeded",
                CreatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(payment, cancellationToken);

            await publisher.PublishAsync("payment.succeeded",
                new PaymentSucceededIntegrationEvent(
                    payment.Id.Value,
                    payment.BookingId,
                    payment.UserId,
                    payment.Amount,
                    payment.Currency,
                    DateTime.UtcNow), cancellationToken);

            return payment.Id.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await publisher.PublishAsync("payment.failed",
                new PaymentFailedIntegrationEvent(
                    paymentId.Value,
                    cmd.BookingId,
                    cmd.UserId,
                    ex.Message,
                    DateTime.UtcNow), cancellationToken);

            throw;
        }
    }
}
