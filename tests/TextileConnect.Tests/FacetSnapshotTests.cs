using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>
/// The filter counts are worked out in memory from a copy of the catalogue (CatalogueFacetSnapshot.cs) and, for a catalogue too large to copy, by SQL.
/// Both must give exactly the same answer for every filter, on a catalogue with the awkward cases: drafts, deleted products, a suspended seller, products without price tiers,
/// inactive variants, a seller that has only a registered address. This is that check, in memory and on a real MySQL server.
/// </summary>
public sealed class FacetSnapshotTests
{
    private static async Task PrepareAsync(MarketplaceDbContext db)
    {
        await CatalogueSeeder.SeedAsync(db, 400, sellers: 8);
        var sellers = await db.Businesses.Where(x => x.LegalName.StartsWith("Seed Seller")).OrderBy(x => x.Id).ToListAsync();
        sellers[0].VerificationStatus = VerificationStatus.Suspended;                                   // its products are not listed
        var onlyRegistered = await db.BusinessAddresses.SingleAsync(x => x.BusinessId == sellers[1].Id);
        onlyRegistered.AddressType = AddressType.Registered;                                             // a seller whose only address is the registered one
        var products = await db.Products.OrderBy(x => x.Id).ToListAsync();
        foreach (var product in products)
        {
            if (product.Id % 11 == 0) product.Status = ProductStatus.Draft;
            if (product.Id % 17 == 0) product.IsDeleted = true;
        }
        await db.SaveChangesAsync();
        var noTiers = products.Where(x => x.Id % 19 == 0).Select(x => x.Id).ToList();
        db.PriceSlabs.RemoveRange(await db.PriceSlabs.Where(x => noTiers.Contains(x.ProductId)).ToListAsync());
        foreach (var variant in await db.ProductVariants.Where(x => x.Id % 7 == 0).ToListAsync()) variant.IsActive = false;
        await db.SaveChangesAsync();
    }

    /// <summary>MySQL compares text without regard to case, the in-memory test store does not, so the queries that rely on that run on MySQL only.</summary>
    private static async Task CompareAsync(MarketplaceDbContext db, bool caseInsensitiveStore)
    {
        await PrepareAsync(db);
        var cityId = await db.BusinessAddresses.OrderBy(x => x.Id).Skip(2).Select(x => x.CityId).FirstAsync();
        var city = await db.Cities.SingleAsync(x => x.Id == cityId);
        var state = await db.States.SingleAsync(x => x.Id == city.StateId);
        var categoryId = await db.Categories.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();

        var queries = new List<CatalogueQuery>
        {
            new(), new(Q: "jacket"), new(Q: "quilted wool"), new(Q: "xl"), new(Q: "nothingmatchesthis"), new(Q: "WOOL"),
            new(CategoryId: categoryId), new(Material: "Wool"), new(Material: "ool"), new(Size: "M"), new(Colour: "Blue"),
            new(MinPrice: 200), new(MaxPrice: 500), new(MinPrice: 150, MaxPrice: 400), new(MaxMoq: 30),
            new(SellerCityId: cityId), new(StateId: state.Id), new(State: state.Code),
            new(Q: "jacket", Material: "Wool", Size: "M", MinPrice: 200),
            new(CategoryId: categoryId, StateId: state.Id, Colour: "Black"),
            new(Q: "knitted", SellerCityId: cityId, Season: "Winter", MaxMoq: 60),
            new(Size: "XL", Colour: "Red", MinPrice: 100, MaxPrice: 900, Sort: "price-asc"),
        };
        if (caseInsensitiveStore)
            queries.AddRange([new(Material: "wool"), new(Season: "winter"), new(Size: "m"), new(Colour: "blue"), new(State: state.Name.ToLowerInvariant())]);
        foreach (var query in queries)
        {
            var fromDatabase = await new CatalogueReader(db).FacetsFromDatabaseAsync(query, default);
            var fromSnapshot = await new CatalogueReader(db).FacetsFromSnapshotAsync(query, default);
            Assert.NotNull(fromSnapshot);
            Assert.True(JsonSerializer.Serialize(fromDatabase) == JsonSerializer.Serialize(fromSnapshot),
                $"The counts differ for {query}\nSQL:      {JsonSerializer.Serialize(fromDatabase)}\nSnapshot: {JsonSerializer.Serialize(fromSnapshot)}");
        }
        // The list and the counts agree about which products exist.
        var all = await new CatalogueReader(db).FacetsFromSnapshotAsync(new(), default);
        Assert.Equal((await new CatalogueReader(db).ListAsync(new(PageSize: 1), default)).Total, all!.Total);
        Assert.True(all.Total > 100 && all.Total < 400);                // some products were left out on purpose
    }

    [Fact]
    public async Task The_snapshot_and_the_sql_give_the_same_counts_in_memory()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        await CompareAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(), caseInsensitiveStore: false);
    }

    [MySqlFact]
    public Task The_snapshot_and_the_sql_give_the_same_counts_on_mysql() => MySqlStore.RunAsync(async () =>
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        await CompareAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(), caseInsensitiveStore: true);
    });

    [Fact]
    public async Task A_copy_kept_in_the_cache_is_reused_and_a_second_reader_sees_the_same_counts()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await CatalogueSeeder.SeedAsync(db, 60, sellers: 3);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var first = await new CatalogueReader(db, cache).FacetsAsync(new(), default);
        db.Products.RemoveRange(db.Products);                                   // the catalogue empties, the kept copy does not know yet
        await db.SaveChangesAsync();
        var second = await new CatalogueReader(db, cache).FacetsAsync(new(), default);
        var withoutCache = await new CatalogueReader(db).FacetsAsync(new(), default);

        Assert.Equal(60, first.Total);
        Assert.Equal(60, second.Total);                                          // answered from the copy: up to CatalogueReader.SnapshotLifetime old
        Assert.Equal(0, withoutCache.Total);                                      // no cache, always the present state
    }
}
