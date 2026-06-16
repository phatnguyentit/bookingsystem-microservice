using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.CatalogService.Infrastructure.Persistence;

public class CatalogDbContextFactory : DesignTimeDbContextFactoryBase<CatalogDbContext>
{
    protected override string ConnectionName => "catalogdb";
    protected override string ApiProjectName => "BookingSystem.CatalogService.Api";
}
