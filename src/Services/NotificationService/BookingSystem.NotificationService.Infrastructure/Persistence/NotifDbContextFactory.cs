using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingSystem.NotificationService.Infrastructure.Persistence;

public class NotifDbContextFactory : IDesignTimeDbContextFactory<NotifDbContext>
{
    public NotifDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotifDbContext>()
            .UseNpgsql("notifdb")
            .Options;

        return new NotifDbContext(options);
    }
}
