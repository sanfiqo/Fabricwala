using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;

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
                throw new InvalidOperationException("Application Identity persistence is not configured.");

            MySqlConnectionStringBuilder connection;
            try { connection = new MySqlConnectionStringBuilder(value); }
            catch { throw new InvalidOperationException("Application Identity configuration is invalid."); }
            if (connection.Server is not ("localhost" or "127.0.0.1" or "::1")
                || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.UserID)
                || string.IsNullOrWhiteSpace(connection.Password)
                || connection.UserID.Equals("root", StringComparison.OrdinalIgnoreCase)
                || connection.UserID.Contains("providercheck", StringComparison.OrdinalIgnoreCase)
                || connection.UserID.Contains("vyntrona", StringComparison.OrdinalIgnoreCase)
                || connection.Database.Contains("providercheck", StringComparison.OrdinalIgnoreCase)
                || connection.Database.Contains("vyntrona", StringComparison.OrdinalIgnoreCase)
                || new[] { "mysql", "sys", "information_schema", "performance_schema" }
                    .Contains(connection.Database, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("A dedicated local application database and non-root account are required.");

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
