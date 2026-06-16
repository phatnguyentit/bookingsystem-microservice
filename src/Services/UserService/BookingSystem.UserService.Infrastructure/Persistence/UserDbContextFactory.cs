using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.UserService.Infrastructure.Persistence;

public class UserDbContextFactory : DesignTimeDbContextFactoryBase<UserDbContext>
{
    protected override string ConnectionName => "userdb";
    protected override string ApiProjectName => "BookingSystem.UserService.Api";
}
