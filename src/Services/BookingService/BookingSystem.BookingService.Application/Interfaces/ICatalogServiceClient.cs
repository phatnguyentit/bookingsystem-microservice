using BookingSystem.Shared.Contracts.DTOs;

namespace BookingSystem.BookingService.Application.Interfaces;

public interface ICatalogServiceClient
{
    Task<CatalogDto?> GetCatalogAsync(Guid catalogId, CancellationToken cancellationToken = default);
}
