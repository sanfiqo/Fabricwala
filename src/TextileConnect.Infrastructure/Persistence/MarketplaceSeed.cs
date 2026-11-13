using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Infrastructure.Persistence;

public static class MarketplaceSeed
{
    public static async Task SeedAsync(MarketplaceDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var stateData in IndiaMasterData.States)
        {
            var state = await db.States.SingleOrDefaultAsync(x => x.Code == stateData.Code, cancellationToken);
            if (state is null)
            {
                state = new State { Name = stateData.Name, Code = stateData.Code };
                db.States.Add(state);
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var cityName in stateData.Cities)
                if (!await db.Cities.AnyAsync(x => x.StateId == state.Id && x.Name == cityName, cancellationToken))
                    db.Cities.Add(new City { StateId = state.Id, Name = cityName, IsDeliveryZone = true, IsSourcingHub = cityName is "Surat" or "Delhi" or "Ludhiana" or "Panipat" or "Jaipur" or "Tiruppur" or "Ahmedabad" or "Mumbai" });
        }

        foreach (var zoneData in IndiaMasterData.Zones)
            if (!await db.DeliveryZones.AnyAsync(x => x.PincodePrefix == zoneData.Prefix, cancellationToken))
                db.DeliveryZones.Add(new DeliveryZone { Name = zoneData.Name, PincodePrefix = zoneData.Prefix });

        foreach (var name in CategorySeed.Names)
        {
            var slug = name.ToLowerInvariant().Replace("'", string.Empty).Replace(' ', '-');
            if (!await db.Categories.AnyAsync(x => x.Slug == slug, cancellationToken))
                db.Categories.Add(new Category { Name = name, Slug = slug, SortOrder = Array.IndexOf(CategorySeed.Names, name) });
        }

        var settings = new Dictionary<string, string>
        {
            ["Commission.DefaultRate"] = "0.02",
            ["Retailer.PlatformFeeRate"] = "0",
            ["Settlement.AutoReleaseDays"] = "7",
            ["Brand.Name"] = "TextileConnect"
        };
        foreach (var setting in settings)
            if (!await db.PlatformSettings.AnyAsync(x => x.Key == setting.Key, cancellationToken))
                db.PlatformSettings.Add(new PlatformSetting { Key = setting.Key, Value = setting.Value });

        await db.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"Marketplace seed counts: States={await db.States.CountAsync(cancellationToken)}, Cities={await db.Cities.CountAsync(cancellationToken)}, Zones={await db.DeliveryZones.CountAsync(cancellationToken)}, Categories={await db.Categories.CountAsync(cancellationToken)}, PlatformSettings={await db.PlatformSettings.CountAsync(cancellationToken)}");
    }
}
