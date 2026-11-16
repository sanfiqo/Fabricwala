using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sanfiqo.Identity;
using TextileConnect.Api;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class DevelopmentSeedTests
{
    private static async Task<WebApplication> HostAsync(bool withKey = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(withKey ? new Dictionary<string, string?> { ["UserContacts:LookupHmacKey"] = new string('k', 64) } : []); // a fake test key
        builder.Logging.ClearProviders();
        var identityName = Guid.NewGuid().ToString(); var marketName = Guid.NewGuid().ToString();
        builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(identityName));
        builder.Services.AddDbContext<MarketplaceDbContext>(options => options.UseInMemoryDatabase(marketName));
        builder.Services.AddScoped<ISellerProductService, EfSellerProductService>();
        var app = builder.Build();
        await SanfiqoRoleSeeder.SeedAsync(app.Services);
        using var scope = app.Services.CreateScope();
        await MarketplaceSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>());
        return app;
    }

    [Fact]
    public async Task Seed_creates_the_design_data_once_and_is_safe_to_repeatAsync()
    {
        await using var app = await HostAsync();
        await DevelopmentSeed.SeedAsync(app.Services);
        await DevelopmentSeed.SeedAsync(app.Services); // a second start must not duplicate anything

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var crypto = scope.ServiceProvider.GetRequiredService<UserContactCryptography>();

        Assert.Equal(7, await identity.Users.CountAsync());
        Assert.Equal(6, await db.Businesses.CountAsync());
        foreach (var persona in DevelopmentSeed.Personas)
        {
            // Sign-in finds the user through the same encrypted owner key the OTP flow uses.
            Assert.True(UserContactNormalizer.TryNormalize(UserContactKind.Mobile, persona.Phone, out var mobile));
            var key = crypto.CreateLookupKey(mobile);
            var contact = await identity.UserContacts.SingleAsync(x => x.OwnershipKey == key);
            var user = (await users.FindByIdAsync(contact.UserId))!;
            Assert.True(user.IsActive);
            Assert.Equal(new[] { persona.Role }, await users.GetRolesAsync(user));
            if (persona.Type is not null) Assert.Single(await db.BusinessUsers.Where(x => x.UserId == user.Id).ToListAsync());
        }

        var sharma = await db.Businesses.SingleAsync(x => x.LegalName == "Sharma Textiles");
        Assert.Equal(BusinessType.Retailer, sharma.BusinessType);
        Assert.Equal(VerificationStatus.Approved, sharma.VerificationStatus);
        Assert.Equal("Indore", await db.BusinessAddresses.Where(x => x.BusinessId == sharma.Id && x.AddressType == AddressType.Delivery).Join(db.Cities, a => a.CityId, c => c.Id, (a, c) => c.Name).SingleAsync());
        var kapoor = await db.Businesses.SingleAsync(x => x.LegalName == "Kapoor Knits");
        Assert.Equal("Ludhiana", await db.BusinessAddresses.Where(x => x.BusinessId == kapoor.Id && x.AddressType == AddressType.Pickup).Join(db.Cities, a => a.CityId, c => c.Id, (a, c) => c.Name).SingleAsync());
        Assert.Equal(VerificationStatus.Pending, (await db.Businesses.SingleAsync(x => x.LegalName == "Raj Woollens")).VerificationStatus);
        Assert.All(new[] { "Shree Kurti House", "Gupta Garments", "Mehta Stores" }, name => Assert.True(db.Businesses.Any(x => x.LegalName == name && x.VerificationStatus == VerificationStatus.Approved)));

        var theirs = await db.Products.Where(x => x.SellerBusinessId == kapoor.Id).ToListAsync();
        Assert.Equal(new[] { "Fleece hoodie", "Quilted puffer jacket", "Sherpa jacket", "Wool cardigan" }, theirs.Select(x => x.Title).OrderBy(x => x).ToArray());
        Assert.Equal(ProductStatus.Draft, theirs.Single(x => x.Title == "Wool cardigan").Status);
        Assert.All(theirs.Where(x => x.Title != "Wool cardigan"), x => Assert.Equal(ProductStatus.Active, x.Status));
        var puffer = theirs.Single(x => x.Title == "Quilted puffer jacket");
        var tiers = await db.PriceSlabs.Where(x => x.ProductId == puffer.Id).OrderBy(x => x.MinQty).Select(x => new { x.MinQty, x.PricePerUnit }).ToListAsync();
        Assert.Equal(new[] { (50, 480m), (200, 459m), (500, 440m) }, tiers.Select(x => (x.MinQty, x.PricePerUnit)).ToArray());
        Assert.Equal(50, puffer.Moq);
        Assert.Equal(6, await db.Products.CountAsync());
        Assert.True(await db.DeliveryZoneRates.CountAsync() >= 4); // freight exists for every seller/retailer zone pair
    }

    [Fact]
    public async Task Without_the_contact_key_businesses_and_products_are_seeded_and_users_are_linked_on_the_next_startAsync()
    {
        await using var app = await HostAsync(withKey: false);
        await DevelopmentSeed.SeedAsync(app.Services); // must not throw
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(6, await db.Businesses.CountAsync());
        Assert.Equal(6, await db.Products.CountAsync());
        Assert.Equal(0, await db.BusinessUsers.CountAsync());
    }
}
