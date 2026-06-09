using BookingSystem.CatalogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.CatalogService.Infrastructure.Repositories;

public class ListingRepository(CatalogDbContext db) : IListingRepository
{
    public Task<Catalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Catalogs.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task AddAsync(Catalog listing, CancellationToken cancellationToken = default)
    {
        await db.Catalogs.AddAsync(listing, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
