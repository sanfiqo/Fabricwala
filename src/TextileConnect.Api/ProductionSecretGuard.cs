using Microsoft.Extensions.Configuration;

namespace TextileConnect.Api;

public static class ProductionSecretGuard
{
    private static readonly string[] DevelopmentHmacKeys =
    [
        "textileconnect-local-lookup-hmac-key-32",
        "textileconnect-local-storage-hmac-key-32"
    ];

    public static void Validate(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
            return;

        var configuredKeys = new[]
        {
            configuration["UserContacts:LookupHmacKey"],
            configuration["Otp:StorageHmacKey"]
        };

        if (configuredKeys.Any(key => key is not null && DevelopmentHmacKeys.Contains(key, StringComparer.Ordinal)))
            throw new InvalidOperationException("Production cannot use local Development HMAC key values.");
    }
}
