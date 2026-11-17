using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sanfiqo.Identity;

namespace TextileConnect.Tests;

/// <summary>
/// The Data Protection keys sign the auth cookie and the anti-forgery token. If they sat on the local disk of one container, a restart or a second instance would sign everybody out
/// or reject every token. They must be in the identity database (docs/SECRETS.md). Encrypting them at rest needs a certificate and is not done (docs/SECURITY_REVIEW.md).
/// </summary>
public sealed class DataProtectionKeysTests
{
    private static WebApplication Build(string databaseName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);   // registers the key ring itself: no override here
        builder.Services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return builder.Build();
    }

    [Fact]
    public async Task The_keys_are_stored_in_the_identity_database()
    {
        await using var app = Build(Guid.NewGuid().ToString());
        var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

        protector.Protect("hello");                                    // making the first key writes it

        using var scope = app.Services.CreateScope();
        var keys = await scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>().Set<DataProtectionKey>().ToListAsync();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key.Xml)));
    }

    [Fact]
    public async Task A_restarted_instance_reads_the_same_keys_and_opens_what_the_first_one_sealed()
    {
        var database = Guid.NewGuid().ToString();
        string sealedText;
        await using (var first = Build(database))
            sealedText = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Protect("session-42");

        await using var second = Build(database);                      // a new process with the same database
        Assert.Equal("session-42", second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Unprotect(sealedText));
    }

    [Fact]
    public async Task A_second_instance_started_at_the_same_time_uses_the_same_key()
    {
        var database = Guid.NewGuid().ToString();
        await using var one = Build(database);
        await using var two = Build(database);
        var sealedByOne = one.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Protect("x");

        Assert.Equal("x", two.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Unprotect(sealedByOne));
    }
}
