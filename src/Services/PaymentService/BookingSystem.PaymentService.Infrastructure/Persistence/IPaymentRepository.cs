using BookingSystem.PaymentService.Infrastructure.Outbox;

namespace BookingSystem.PaymentService.Infrastructure.Persistence;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the captured payment for a booking (status <c>Succeeded</c> or already <c>Refunded</c>),
    /// most recent first. Used by the refund/compensation path. Returns null when no captured
    /// payment exists for the booking.
    /// </summary>
    Task<Payment?> GetCapturedByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>Inserts the payment and commits immediately — records the in-flight attempt.</summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Stages an integration event; it is published only after the next <see cref="SaveChangesAsync"/>.</summary>
    void AddOutboxMessage(OutboxMessage message);

    /// <summary>Commits all tracked changes (status update + staged outbox rows) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
