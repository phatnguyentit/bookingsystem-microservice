using BookingSystem.CatalogService.Api.Features.GetListing;
using BookingSystem.CatalogService.Api.Features.CreateListing;
using MediatR;

namespace BookingSystem.CatalogService.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog");

        group.MapGet("/catalogs/{id:guid}", async (Guid id, ISender sender) =>
        {
            var listing = await sender.Send(new GetCatalogByIdQuery(id));
            return listing is null ? Results.NotFound() : Results.Ok(listing);
        })
        .WithName("GetListing");

        group.MapPost("/catalogs", async (CreateCatalogCommand cmd, ISender sender) =>
        {
            var id = await sender.Send(cmd);
            return Results.Created($"/api/catalog/catalogs/{id}", new { id });
        })
        .WithName("CreateListing");
    }
}
