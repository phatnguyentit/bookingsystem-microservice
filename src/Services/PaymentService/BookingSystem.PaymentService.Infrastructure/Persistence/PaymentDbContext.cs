using BookingSystem.PaymentService.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.PaymentService.Infrastructure.Persistence;

public class Payment
{
    public PaymentId Id { get; set; } = default!;
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Payment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id)
                .HasConversion(id => id.Value, v => new PaymentId(v));
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.Status).HasMaxLength(20);
            e.ToTable("payments");
        });

        mb.Entity<OutboxMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Topic).IsRequired().HasMaxLength(200);
            e.Property(m => m.EventType).IsRequired().HasMaxLength(500);
            e.Property(m => m.Payload).IsRequired();
            e.Property(m => m.Error).HasMaxLength(2000);
            // index for the background processor query: unprocessed messages ordered by creation
            e.HasIndex(m => new { m.ProcessedAt, m.CreatedAt });
            e.ToTable("outbox_messages");
        });
    }
}
