using BookingSystem.CatalogService.Api.Features.GetListing;
using BookingSystem.CatalogService.Infrastructure.Persistence;
using BookingSystem.CatalogService.Infrastructure.Repositories;
using BookingSystem.Shared.Contracts.DTOs;
using FluentAssertions;
using NSubstitute;

namespace CatalogService.Tests.Features;

public class GetCatalogByIdHandlerTests
{
    private readonly IListingRepository _repo = Substitute.For<IListingRepository>();

    [Fact]
    public async Task Handle_ListingNotFound_ReturnsNull()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Catalog?)null);

        var dto = await new GetCatalogByIdHandler(_repo).Handle(new GetCatalogByIdQuery(Guid.NewGuid()), default);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ListingExists_MapsAllFieldsToSharedDto()
    {
        var listing = new Catalog
        {
            Id = Guid.NewGuid(),
            Title = "Beach House",
            Description = "A house on the beach",
            PricePerNight = 150m,
            Currency = "USD",
            IsAvailable = false,
            CreatedAt = DateTime.UtcNow
        };
        _repo.GetByIdAsync(listing.Id, Arg.Any<CancellationToken>()).Returns(listing);

        var dto = await new GetCatalogByIdHandler(_repo).Handle(new GetCatalogByIdQuery(listing.Id), default);

        dto.Should().Be(new CatalogDto(listing.Id, "Beach House", "A house on the beach", 150m, "USD", false));
    }
}
