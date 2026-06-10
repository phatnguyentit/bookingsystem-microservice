using BookingSystem.BookingService.Application.Interfaces;
using BookingSystem.Shared.Contracts.DTOs;
using System.Net.Http.Json;

namespace BookingSystem.BookingService.Infrastructure.HttpClients;

public class CatalogServiceClient(HttpClient http) : ICatalogServiceClient
{
    public Task<CatalogDto?> GetCatalogAsync(Guid catalogId, CancellationToken cancellationToken = default)
        => http.GetFromJsonAsync<CatalogDto>($"/api/catalog/catalogs/{catalogId}", cancellationToken);
}
