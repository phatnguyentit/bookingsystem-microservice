using BookingSystem.BookingService.Application.Interfaces.UoW;
using BookingSystem.BookingService.Domain.Common;
using BookingSystem.BookingService.Infrastructure.Outbox;
using BookingSystem.BookingService.Infrastructure.Persistence;

namespace BookingSystem.BookingService.Infrastructure.Messaging;

public class UnitOfWork(BookingDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Serialize all pending domain events into outbox rows BEFORE saving.
        // Both the business data change and the outbox messages are committed
        // in a single transaction — if the DB save fails, neither is persisted.
        var outboxMessages = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(OutboxMessage.From)
            .ToList();

        if (outboxMessages.Any())
        {
            dbContext.OutboxMessages.AddRange(outboxMessages);
        }

        // Clear events now so re-entrant calls don't double-publish.
        foreach (var entry in dbContext.ChangeTracker.Entries<AggregateRoot>())
        {
            entry.Entity.ClearDomainEvents();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        // Kafka publishing is handled by OutboxProcessor (background service).
        // If Kafka is unavailable the outbox rows remain unprocessed and will
        // be retried on the next polling cycle.
    }
}