using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Payments;

namespace TextileConnect.Infrastructure.Persistence;

public static class MarketplacePersistenceRegistration
{
    public static IServiceCollection AddMarketplacePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MarketplaceDbContext>(options =>
            options.UseMySQL(configuration.GetConnectionString("TextileConnect")
                ?? throw new InvalidOperationException("ConnectionStrings:TextileConnect is required.")));
        services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>();
        return services;
    }
}
