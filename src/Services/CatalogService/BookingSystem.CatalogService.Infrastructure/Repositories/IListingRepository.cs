using BookingSystem.CatalogService.Infrastructure.Persistence;

namespace BookingSystem.CatalogService.Infrastructure.Repositories;

public interface IListingRepository
{
    Task<Catalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Catalog listing, CancellationToken cancellationToken = default);
}
