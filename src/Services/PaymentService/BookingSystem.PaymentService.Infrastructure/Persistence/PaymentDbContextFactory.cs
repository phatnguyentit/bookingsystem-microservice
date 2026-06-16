using BookingSystem.Shared.CrossCutting.Configuration;

namespace BookingSystem.PaymentService.Infrastructure.Persistence;

public class PaymentDbContextFactory : DesignTimeDbContextFactoryBase<PaymentDbContext>
{
    protected override string ConnectionName => "paymentdb";
    protected override string ApiProjectName => "BookingSystem.PaymentService.Api";
}
