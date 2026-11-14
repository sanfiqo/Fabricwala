using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TextileConnect.Infrastructure.Persistence;

// Migration tooling must never start the web app or load its optional local file.
public sealed class MarketplaceDesignTimeFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets("TextileConnect.Api").AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("TextileConnect")
            ?? throw new InvalidOperationException("Set ConnectionStrings:TextileConnect in user-secrets or environment variables for migration tooling.");
        return new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseMySQL(connection.Replace("SslMode=None", "SslMode=Disabled", StringComparison.OrdinalIgnoreCase)).Options);
    }
}
