using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sanfiqo.Identity;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>
/// Demo data that matches the app designs, for local development only (Program calls it in the Development environment, never in Testing or Production).
/// Safe to run on every start: anything that already exists is left alone. All mobile numbers are fake; sign in with the fixed development OTP.
/// </summary>
public static class DevelopmentSeed
{
    public sealed record Persona(string Phone, string FullName, string Role, string BusinessName, BusinessType? Type, string? Pincode, bool Approved, string? Street);

    public static readonly IReadOnlyList<Persona> Personas =
    [
        // The +91999990xxxx range is used so these never collide with phone numbers already stored in a developer database.
        new("+919999900000", "Dev Admin", SanfiqoRoleNames.Admin, "", null, null, true, null),
        new("+919999900001", "Ramesh Sharma", SanfiqoRoleNames.Retailer, "Sharma Textiles", BusinessType.Retailer, "452001", true, "12 Market Road"),
        new("+919999900002", "Anil Kapoor", SanfiqoRoleNames.Manufacturer, "Kapoor Knits", BusinessType.Manufacturer, "141001", true, "Plot 7, Focal Point"),
        new("+919999900011", "Shree Kurti House", SanfiqoRoleNames.Manufacturer, "Shree Kurti House", BusinessType.Manufacturer, "302001", true, "Sanganer Road"),
        new("+919999900012", "Gupta Garments", SanfiqoRoleNames.Manufacturer, "Gupta Garments", BusinessType.Manufacturer, "110001", true, "Gandhi Nagar"),
        new("+919999900013", "Mehta Stores", SanfiqoRoleNames.Retailer, "Mehta Stores", BusinessType.Retailer, "180001", true, "Raghunath Bazaar"),
        // Waiting for admin approval, so the user still has the Retailer role until an admin approves (the role changes on approval).
        new("+919999900014", "Raj Woollens", SanfiqoRoleNames.Retailer, "Raj Woollens", BusinessType.Manufacturer, "141001", false, "Chaura Bazaar"),
    ];

    private static bool warned;

    private static readonly (string Pincode, string City)[] ExtraPostalCodes = [("302001", "Jaipur"), ("110001", "Delhi"), ("180001", "Jammu"), ("190001", "Srinagar")];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeed");
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        log.LogInformation("Development seed: postal codes");
        await SeedPostalCodesAsync(db, ct);
        foreach (var persona in Personas)
        {
            log.LogInformation("Development seed: user and business for {Name} - begin", persona.FullName);
            var userId = await EnsureUserAsync(scope.ServiceProvider, persona, ct);
            if (persona.Type is not null) await EnsureBusinessAsync(db, persona, userId, ct);
            log.LogInformation("Development seed: user and business for {Name} - done", persona.FullName);
        }
        log.LogInformation("Development seed: freight rates");
        await SeedFreightAsync(db, ct);
        var products = scope.ServiceProvider.GetRequiredService<ISellerProductService>();
        log.LogInformation("Development seed: products (each product is checked, and created only if missing)");
        await SeedProductsAsync(db, products, log, ct);
        log.LogInformation("Development seed: finished");
    }

    private static async Task SeedPostalCodesAsync(MarketplaceDbContext db, CancellationToken ct)
    {
        foreach (var (pincode, cityName) in ExtraPostalCodes)
        {
            var city = await db.Cities.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.Name == cityName, ct);
            if (city is not null && !await db.PostalCodes.AnyAsync(x => x.Pincode == pincode, ct)) db.PostalCodes.Add(new PostalCode { Pincode = pincode, CityId = city.Id });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Returns null when the user contact lookup key (UserContacts:LookupHmacKey in user-secrets) is not configured yet.</summary>
    private static async Task<string?> EnsureUserAsync(IServiceProvider services, Persona persona, CancellationToken ct)
    {
        var identity = services.GetRequiredService<ApplicationIdentityDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        UserContactCryptography crypto;
        try { crypto = services.GetRequiredService<UserContactCryptography>(); }
        catch (InvalidOperationException)
        {
            if (!warned) Console.WriteLine("Development seed: demo users were NOT created because UserContacts:LookupHmacKey is not set in user-secrets. Businesses and products were still seeded; restart after setting it.");
            warned = true;
            return null;
        }
        if (!UserContactNormalizer.TryNormalize(UserContactKind.Mobile, persona.Phone, out var mobile)) throw new InvalidOperationException("Invalid development phone number.");
        var ownerKey = crypto.CreateLookupKey(mobile);
        var existing = await identity.UserContacts.SingleOrDefaultAsync(x => x.Kind == UserContactKind.Mobile && x.OwnershipKey == ownerKey, ct);
        if (existing is not null) return existing.UserId;

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString("D"), UserName = $"dev-{mobile.Value.TrimStart('+')}", PhoneNumber = mobile.Value, PhoneNumberConfirmed = true, FullName = persona.FullName, IsActive = true };
        if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddToRoleAsync(user, persona.Role)).Succeeded) throw new InvalidOperationException("Could not create a development user.");
        var now = DateTime.UtcNow;
        identity.UserContacts.Add(new UserContact
        {
            Id = Guid.NewGuid(), UserId = user.Id, Kind = UserContactKind.Mobile, LookupKey = ownerKey, OwnershipKey = ownerKey, ProtectedValue = crypto.ProtectContactValue(mobile.Value),
            IsVerified = true, VerifiedAtUtc = now, IsPrimary = true, CreatedAtUtc = now, Version = 1
        });
        await identity.SaveChangesAsync(ct);
        return user.Id;
    }

    private static async Task EnsureBusinessAsync(MarketplaceDbContext db, Persona persona, string? userId, CancellationToken ct)
    {
        var existing = await db.Businesses.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.LegalName == persona.BusinessName, ct);
        if (existing is not null)
        {
            // Users may have become available after the first run, or the demo phone numbers may have changed since an earlier run:
            // these are demo businesses, so the demo owner is (re)linked and any older demo owner link is replaced.
            if (userId is not null && !await db.BusinessUsers.AnyAsync(x => x.BusinessId == existing.Id && x.UserId == userId, ct))
            {
                db.BusinessUsers.RemoveRange(await db.BusinessUsers.Where(x => x.BusinessId == existing.Id && x.Role == BusinessMemberRole.Owner).ToListAsync(ct));
                db.BusinessUsers.Add(new BusinessUser { BusinessId = existing.Id, UserId = userId, Role = BusinessMemberRole.Owner, IsPrimary = true });
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        var postal = await db.PostalCodes.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.Pincode == persona.Pincode, ct)
            ?? throw new InvalidOperationException($"Pincode {persona.Pincode} is not in the postal directory.");
        var now = DateTime.UtcNow;
        var business = new Business
        {
            BusinessType = persona.Type!.Value, LegalName = persona.BusinessName, DisplayName = persona.BusinessName, Phone = persona.Phone,
            VerificationStatus = persona.Approved ? VerificationStatus.Approved : VerificationStatus.Pending, ApprovedAtUtc = persona.Approved ? now : null, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Businesses.Add(business);
        await db.SaveChangesAsync(ct);
        if (userId is not null) db.BusinessUsers.Add(new BusinessUser { BusinessId = business.Id, UserId = userId, Role = BusinessMemberRole.Owner, IsPrimary = true });
        db.BusinessAddresses.Add(new BusinessAddress { BusinessId = business.Id, AddressType = AddressType.Registered, Line1 = persona.Street!, CityId = postal.CityId, Pincode = persona.Pincode!, IsDefault = true });
        db.BusinessAddresses.Add(new BusinessAddress
        {
            BusinessId = business.Id, AddressType = persona.Type == BusinessType.Manufacturer ? AddressType.Pickup : AddressType.Delivery,
            Line1 = persona.Street!, CityId = postal.CityId, Pincode = persona.Pincode!, IsDefault = true
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Per-piece freight between the zones the demo businesses use, so every landed price and checkout works out of the box.</summary>
    private static async Task SeedFreightAsync(MarketplaceDbContext db, CancellationToken ct)
    {
        var zones = await db.DeliveryZones.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        long? ZoneOf(string pincode) => zones.Where(z => pincode.StartsWith(z.PincodePrefix, StringComparison.Ordinal)).OrderByDescending(z => z.PincodePrefix.Length).FirstOrDefault()?.Id;
        var sellers = Personas.Where(x => x.Type == BusinessType.Manufacturer).Select(x => ZoneOf(x.Pincode!)).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var buyers = Personas.Where(x => x.Type == BusinessType.Retailer).Select(x => ZoneOf(x.Pincode!)).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        foreach (var from in sellers)
            foreach (var to in buyers)
                if (!await db.DeliveryZoneRates.AnyAsync(x => x.FromZoneId == from && x.ToZoneId == to && x.RateType == ShippingRateType.PerPiece, ct))
                    db.DeliveryZoneRates.Add(new DeliveryZoneRate { FromZoneId = from, ToZoneId = to, RateType = ShippingRateType.PerPiece, Rate = from == to ? 12m : 22m, MinCharge = 800m, TransitDays = from == to ? 2 : 5 });
        await db.SaveChangesAsync(ct);
    }

    private sealed record Demo(string Seller, string Title, string Category, string Material, int Moq, decimal Gst, (int Min, decimal Price)[] Tiers, (string Size, string Colour, int Stock)[] Variants, bool Publish);

    private static async Task SeedProductsAsync(MarketplaceDbContext db, ISellerProductService products, ILogger log, CancellationToken ct)
    {
        Demo[] demos =
        [
            new("Kapoor Knits", "Quilted puffer jacket", "winter-wear", "Polyester", 50, 5, [(50, 480m), (200, 459m), (500, 440m)],
                [("M", "Black", 600), ("L", "Black", 800), ("XL", "Black", 400), ("L", "Navy", 350)], true),
            new("Kapoor Knits", "Fleece hoodie", "winter-wear", "Fleece", 30, 5, [(30, 360m), (100, 340m)], [("M", "Grey", 500), ("L", "Grey", 500), ("L", "Maroon", 3)], true),
            new("Kapoor Knits", "Sherpa jacket", "winter-wear", "Sherpa", 40, 5, [(40, 520m), (150, 495m)], [("M", "Beige", 300), ("L", "Beige", 300)], true),
            new("Kapoor Knits", "Wool cardigan", "winter-wear", "Wool", 25, 5, [(25, 410m), (100, 390m)], [("M", "Camel", 120)], false),
            new("Shree Kurti House", "Cotton kurti set", "womens", "Cotton", 20, 5, [(20, 300m), (100, 280m)], [("M", "Blue", 400), ("L", "Blue", 400), ("L", "Pink", 250)], true),
            new("Gupta Garments", "Thermal innerwear set", "innerwear", "Cotton blend", 50, 5, [(50, 180m), (200, 165m)], [("M", "White", 900), ("L", "White", 900)], true),
        ];
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(x => x.Slug, x => x.Id, ct);
        foreach (var demo in demos)
        {
            var seller = await db.Businesses.AsNoTracking().OrderBy(x => x.Id).FirstAsync(x => x.LegalName == demo.Seller, ct);
            if (await db.Products.AnyAsync(x => x.SellerBusinessId == seller.Id && x.Title == demo.Title && !x.IsDeleted, ct)) { log.LogInformation("Development seed: product {Title} already exists", demo.Title); continue; }
            log.LogInformation("Development seed: creating product {Title}", demo.Title);
            await products.CreateAsync(seller.Id, new SellerProductRequest(demo.Title, categories[demo.Category], $"{demo.Title}, wholesale pack.", demo.Material, null, "Winter", "6101", 700, 3, "7 day return for damaged goods",
                demo.Gst, demo.Moq, ProductUnit.Piece, demo.Variants.Select(v => new SellerVariantInput(null, null, v.Size, v.Colour, v.Stock)).ToList(),
                demo.Tiers.Select(t => new SellerTierInput(t.Min, t.Price)).ToList(), demo.Publish), ct);
        }
    }
}
