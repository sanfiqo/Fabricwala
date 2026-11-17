using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sanfiqo.Identity;

public static class IdentityPersistenceRegistration
{
    public static IServiceCollection AddApplicationIdentityPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationIdentityDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var value = configuration.GetConnectionString("TextileConnect");
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("ConnectionStrings:TextileConnect is required.");

            options.UseMySQL(value.Replace("SslMode=None", "SslMode=Disabled", StringComparison.OrdinalIgnoreCase))
                // EF event messages can contain SQL/provider details even without parameter logging.
                .UseLoggerFactory(NullLoggerFactory.Instance)
                .EnableSensitiveDataLogging(false).EnableDetailedErrors(false);
            // No automatic retry, EnsureCreated, startup migration or connection opening.
        });

        // AddIdentityCore does not register cookie handlers. Web owns the existing scheme.
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Stores.MaxLengthForKeys = 128;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version1;
        }).AddRoles<ApplicationRole>()
          .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
          .AddSignInManager()
          .AddDefaultTokenProviders(); // the authenticator (TOTP) provider of the admin second step
        // Web supplies the canonical principal factory and per-request cookie revalidation.
        return services;
    }
}
