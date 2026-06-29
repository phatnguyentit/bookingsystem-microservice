using MediatR;

namespace BookingSystem.PaymentService.Api.Features.RefundPayment;

/// <summary>
/// Reverses the captured payment for a booking whose confirmation permanently failed.
/// Triggered by the <c>booking.confirmation.failed</c> compensation event.
/// </summary>
public record RefundPaymentCommand(Guid BookingId, string Reason) : IRequest;
