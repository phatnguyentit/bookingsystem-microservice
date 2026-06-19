using MediatR;

namespace BookingSystem.PaymentService.Api.Features.ProcessPayment;

public record ProcessPaymentCommand(
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string PaymentMethod) : IRequest<Guid>;
