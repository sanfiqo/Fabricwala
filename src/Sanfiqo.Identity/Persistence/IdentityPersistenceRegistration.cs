using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace Sanfiqo.Identity;

public static class IdentityPersistenceRegistration
{
    public static IServiceCollection AddApplicationIdentityPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationIdentityDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var identityOptions = provider.GetRequiredService<IOptions<SanfiqoIdentityOptions>>().Value;
            var value = configuration.GetConnectionString(identityOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Application Identity persistence is not configured.");

            MySqlConnectionStringBuilder connection;
            try { connection = new MySqlConnectionStringBuilder(value); }
            catch { throw new InvalidOperationException("Application Identity configuration is invalid."); }
            if (string.IsNullOrWhiteSpace(connection.Server)
                || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.UserID)
                || string.IsNullOrWhiteSpace(connection.Password))
                throw new InvalidOperationException("A complete application database connection is required.");

            options.UseMySQL(connection.ConnectionString)
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
          .AddSignInManager();
        // Web supplies the canonical principal factory and per-request cookie revalidation.
        return services;
    }
}
