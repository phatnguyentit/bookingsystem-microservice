using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.ReviewService.Infrastructure.Persistence;

public class ReviewDbContextFactory : DesignTimeDbContextFactoryBase<ReviewDbContext>
{
    protected override string ConnectionName => "reviewdb";
    protected override string ApiProjectName => "BookingSystem.ReviewService.Api";
}
