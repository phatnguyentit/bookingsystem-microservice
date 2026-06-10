using BookingSystem.BookingService.Domain;
using BookingSystem.BookingService.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.BookingService.Infrastructure.Persistence;

public class BookingDbContext(DbContextOptions<BookingDbContext> options)
    : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
