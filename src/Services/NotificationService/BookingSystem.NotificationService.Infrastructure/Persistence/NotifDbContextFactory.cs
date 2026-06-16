using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.NotificationService.Infrastructure.Persistence;

public class NotifDbContextFactory : DesignTimeDbContextFactoryBase<NotifDbContext>
{
    protected override string ConnectionName => "notifdb";
    protected override string ApiProjectName => "BookingSystem.NotificationService.Api";
}
