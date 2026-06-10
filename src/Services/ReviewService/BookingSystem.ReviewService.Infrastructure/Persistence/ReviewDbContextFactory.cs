using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingSystem.ReviewService.Infrastructure.Persistence;

public class ReviewDbContextFactory : IDesignTimeDbContextFactory<ReviewDbContext>
{
    public ReviewDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReviewDbContext>()
            .UseNpgsql("reviewdb")
            .Options;

        return new ReviewDbContext(options);
    }
}
