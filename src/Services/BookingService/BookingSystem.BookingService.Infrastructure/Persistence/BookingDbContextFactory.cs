using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.BookingService.Infrastructure.Persistence;

public class BookingDbContextFactory : DesignTimeDbContextFactoryBase<BookingDbContext>
{
    protected override string ConnectionName => "bookingdb";
    protected override string ApiProjectName => "BookingSystem.BookingService.Api";
}
