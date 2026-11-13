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
            options.AddInterceptors(new AuditSaveChangesInterceptor()).UseMySQL(
                (configuration.GetConnectionString("TextileConnect")
                    ?? throw new InvalidOperationException("ConnectionStrings:TextileConnect is required.")).Replace("SslMode=None", "SslMode=Disabled", StringComparison.OrdinalIgnoreCase)));
        services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>();
        return services;
    }
}
