using BookingSystem.PaymentService.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.PaymentService.Infrastructure.Persistence;

public class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default)
        => db.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> GetCapturedByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        => db.Payments
            .Where(p => p.BookingId == bookingId &&
                        (p.Status == PaymentStatus.Succeeded ||
                         p.Status == PaymentStatus.RefundPending ||
                         p.Status == PaymentStatus.Refunded ||
                         p.Status == PaymentStatus.RefundFailed))
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await db.Payments.AddAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public void AddOutboxMessage(OutboxMessage message)
        => db.OutboxMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
