using BookingSystem.CatalogService.Infrastructure.Repositories;
using BookingSystem.Shared.Contracts.DTOs;
using MediatR;

namespace BookingSystem.CatalogService.Api.Features.GetListing;

public record GetCatalogByIdQuery(Guid CatalogId) : IRequest<CatalogDto?>;

public class GetCatalogByIdHandler(IListingRepository repo)
    : IRequestHandler<GetCatalogByIdQuery, CatalogDto?>
{
    public async Task<CatalogDto?> Handle(GetCatalogByIdQuery query, CancellationToken cancellationToken)
    {
        var listing = await repo.GetByIdAsync(query.CatalogId, cancellationToken);
        return listing is null ? null
            : new CatalogDto(
                listing.Id,
                listing.Title,
                listing.Description,
                listing.PricePerNight,
                listing.Currency,
                listing.IsAvailable);
    }
}
