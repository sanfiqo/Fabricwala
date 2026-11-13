using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class ProductionSecretGuardTests
{
    [Fact]
    public void Production_rejects_local_lookup_key()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["UserContacts:LookupHmacKey"] = "textileconnect-local-lookup-hmac-key-32",
            ["Otp:StorageHmacKey"] = "production-storage-key"
        });

        Assert.Throws<InvalidOperationException>(() => ProductionSecretGuard.Validate(configuration, Environments.Production));
    }

    [Fact]
    public void Production_rejects_local_storage_key()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["UserContacts:LookupHmacKey"] = "production-lookup-key",
            ["Otp:StorageHmacKey"] = "textileconnect-local-storage-hmac-key-32"
        });

        Assert.Throws<InvalidOperationException>(() => ProductionSecretGuard.Validate(configuration, Environments.Production));
    }

    [Fact]
    public void Development_may_use_local_keys()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["UserContacts:LookupHmacKey"] = "textileconnect-local-lookup-hmac-key-32",
            ["Otp:StorageHmacKey"] = "textileconnect-local-storage-hmac-key-32"
        });

        ProductionSecretGuard.Validate(configuration, Environments.Development);
    }

    [Fact]
    public void Production_accepts_distinct_keys()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["UserContacts:LookupHmacKey"] = "production-lookup-key",
            ["Otp:StorageHmacKey"] = "production-storage-key"
        });

        ProductionSecretGuard.Validate(configuration, Environments.Production);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
