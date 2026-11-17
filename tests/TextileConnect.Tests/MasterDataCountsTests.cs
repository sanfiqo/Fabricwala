using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>What a fresh database really contains after the startup seed. docs/MASTER_DATA.md quotes these numbers.</summary>
public sealed class MasterDataCountsTests
{
    [Fact]
    public async Task A_fresh_database_has_the_states_a_short_city_list_nine_zones_and_only_two_pincodesAsync()
    {
        await using var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await MarketplaceSeed.SeedAsync(db);
        await MarketplaceSeed.SeedAsync(db); // running the seed again adds nothing

        var states = await db.States.CountAsync();
        var cities = await db.Cities.CountAsync();
        var zones = await db.DeliveryZones.CountAsync();
        var pincodes = await db.PostalCodes.CountAsync();

        Assert.Equal(36, states);                                  // 28 states and 8 union territories
        Assert.Equal(IndiaMasterData.States.Sum(x => x.Cities.Length), cities);
        Assert.Equal(9, zones);                                    // the nine first-digit areas of the postal code
        Assert.Equal(2, pincodes);                                 // 452001 Indore and 141001 Ludhiana: the all-India directory is NOT loaded by the seed
        Assert.Equal(new[] { "141001", "452001" }, await db.PostalCodes.OrderBy(x => x.Pincode).Select(x => x.Pincode).ToListAsync());
        Assert.True(cities is > 80 and < 200, $"{cities} cities");
    }

    [Fact]
    public async Task The_sourcing_hubs_are_one_list_and_they_are_flagged_on_the_citiesAsync()
    {
        await using var db = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await MarketplaceSeed.SeedAsync(db);

        var hubs = await db.Cities.Where(x => x.IsSourcingHub).Select(x => x.Name).Distinct().OrderBy(x => x).ToListAsync();
        Assert.Equal(IndiaMasterData.SourcingHubs.OrderBy(x => x).ToList(), hubs);
        Assert.All(IndiaMasterData.SourcingHubs, hub => Assert.Contains(IndiaMasterData.States, s => s.Cities.Contains(hub)));
    }
}
