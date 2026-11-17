using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>
/// Product.PriceFrom and PriceTo are the lowest and highest price of the price tiers, kept in step by MarketplaceDbContext on every save so that a list can sort and filter by price
/// without reading the tiers of every product. Whatever way the tiers are written, the columns must follow.
/// </summary>
public sealed class ProductPriceColumnsTests
{
    private static MarketplaceDbContext NewContext(string database) => new(new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(database).Options);

    private static async Task<long> ProductAsync(MarketplaceDbContext db, string slug = "p")
    {
        var category = await db.Categories.FirstOrDefaultAsync();
        if (category is null) { category = new Category { Name = "Wear", Slug = "wear" }; db.Categories.Add(category); await db.SaveChangesAsync(); }
        var product = new Product { SellerBusinessId = 1, CategoryId = category.Id, Title = slug, Slug = slug, Moq = 10, Status = ProductStatus.Active };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static PriceSlab Slab(long productId, int minQty, decimal price) => new() { ProductId = productId, MinQty = minQty, PricePerUnit = price };

    private static async Task<(decimal? From, decimal? To)> ColumnsAsync(string database, long productId)
    {
        await using var db = NewContext(database);
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Id == productId);
        return (product.PriceFrom, product.PriceTo);
    }

    [Fact]
    public async Task A_new_product_without_tiers_has_no_price_and_gets_one_with_its_first_tier()
    {
        var database = Guid.NewGuid().ToString();
        await using var db = NewContext(database);
        var id = await ProductAsync(db);
        Assert.Equal((null, null), await ColumnsAsync(database, id));

        db.PriceSlabs.AddRange(Slab(id, 10, 400m), Slab(id, 50, 350m), Slab(id, 100, 300m));
        await db.SaveChangesAsync();

        Assert.Equal((300m, 400m), await ColumnsAsync(database, id));
    }

    [Fact]
    public async Task A_product_and_its_tiers_saved_together_are_in_step()
    {
        var database = Guid.NewGuid().ToString();
        await using var db = NewContext(database);
        var category = new Category { Name = "Wear", Slug = "wear" }; db.Categories.Add(category); await db.SaveChangesAsync();
        db.Products.Add(new Product { Id = 77, SellerBusinessId = 1, CategoryId = category.Id, Title = "x", Slug = "x", Moq = 10, Status = ProductStatus.Active });
        db.PriceSlabs.AddRange(Slab(77, 10, 120m), Slab(77, 100, 95m));
        await db.SaveChangesAsync();

        Assert.Equal((95m, 120m), await ColumnsAsync(database, 77));
    }

    [Fact]
    public async Task Changing_one_tier_changes_the_columns_even_from_another_context()
    {
        var database = Guid.NewGuid().ToString();
        long id;
        await using (var first = NewContext(database))
        {
            id = await ProductAsync(first);
            first.PriceSlabs.AddRange(Slab(id, 10, 400m), Slab(id, 100, 300m));
            await first.SaveChangesAsync();
        }

        await using (var second = NewContext(database))            // a later request: the product is not loaded, only a tier is
        {
            var cheapest = await second.PriceSlabs.SingleAsync(x => x.ProductId == id && x.MinQty == 100);
            cheapest.PricePerUnit = 250m;
            await second.SaveChangesAsync();
        }
        Assert.Equal((250m, 400m), await ColumnsAsync(database, id));

        await using (var third = NewContext(database))
        {
            third.PriceSlabs.Remove(await third.PriceSlabs.SingleAsync(x => x.ProductId == id && x.MinQty == 10));
            await third.SaveChangesAsync();
        }
        Assert.Equal((250m, 250m), await ColumnsAsync(database, id));
    }

    [Fact]
    public async Task Replacing_every_tier_as_the_seller_screen_does_follows_and_removing_all_clears_the_price()
    {
        var database = Guid.NewGuid().ToString();
        await using var db = NewContext(database);
        var id = await ProductAsync(db);
        db.PriceSlabs.AddRange(Slab(id, 10, 400m), Slab(id, 100, 300m));
        await db.SaveChangesAsync();

        await using (var edit = NewContext(database))
        {
            edit.PriceSlabs.RemoveRange(await edit.PriceSlabs.Where(x => x.ProductId == id).ToListAsync());     // remove the old tiers and add the new ones in one save
            edit.PriceSlabs.AddRange(Slab(id, 20, 520m), Slab(id, 200, 480m));
            await edit.SaveChangesAsync();
        }
        Assert.Equal((480m, 520m), await ColumnsAsync(database, id));

        await using (var clear = NewContext(database))
        {
            clear.PriceSlabs.RemoveRange(await clear.PriceSlabs.Where(x => x.ProductId == id).ToListAsync());
            await clear.SaveChangesAsync();
        }
        Assert.Equal((null, null), await ColumnsAsync(database, id));
    }

    [Fact]
    public async Task Two_products_in_one_save_each_get_their_own_prices_and_untouched_ones_are_left_alone()
    {
        var database = Guid.NewGuid().ToString();
        await using var db = NewContext(database);
        var a = await ProductAsync(db, "a"); var b = await ProductAsync(db, "b"); var c = await ProductAsync(db, "c");
        db.PriceSlabs.Add(Slab(c, 10, 70m)); await db.SaveChangesAsync();

        db.PriceSlabs.AddRange(Slab(a, 10, 100m), Slab(a, 50, 90m), Slab(b, 10, 500m));
        await db.SaveChangesAsync();

        Assert.Equal((90m, 100m), await ColumnsAsync(database, a));
        Assert.Equal((500m, 500m), await ColumnsAsync(database, b));
        Assert.Equal((70m, 70m), await ColumnsAsync(database, c));
    }

    [Fact]
    public async Task The_list_follows_the_columns_a_product_without_tiers_is_not_listed_and_a_price_filter_uses_the_lowest_price()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Businesses.Add(new Business { Id = 1, BusinessType = BusinessType.Manufacturer, LegalName = "S", DisplayName = "S", Phone = "+919000000001", VerificationStatus = VerificationStatus.Approved });
        await db.SaveChangesAsync();
        var cheap = await ProductAsync(db, "cheap"); var dear = await ProductAsync(db, "dear"); var none = await ProductAsync(db, "none");
        db.PriceSlabs.AddRange(Slab(cheap, 10, 100m), Slab(dear, 10, 900m), Slab(dear, 100, 800m));
        await db.SaveChangesAsync();
        using var client = await fixture.ClientAsync(null);

        var all = await client.GetFromJsonAsyncSafe("/api/v1/products");
        Assert.Equal(2, all.GetProperty("total").GetInt32());                                          // "none" has no tier
        Assert.DoesNotContain(all.GetProperty("items").EnumerateArray(), x => x.GetProperty("slug").GetString() == "none");
        Assert.Equal("cheap", (await client.GetFromJsonAsyncSafe("/api/v1/products?sort=price-asc")).GetProperty("items")[0].GetProperty("slug").GetString());
        Assert.Equal("dear", (await client.GetFromJsonAsyncSafe("/api/v1/products?sort=price-desc")).GetProperty("items")[0].GetProperty("slug").GetString());
        Assert.Equal(800m, (await client.GetFromJsonAsyncSafe("/api/v1/products?sort=price-desc")).GetProperty("items")[0].GetProperty("priceFrom").GetDecimal());
        Assert.Equal(1, (await client.GetFromJsonAsyncSafe("/api/v1/products?minPrice=500")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await client.GetFromJsonAsyncSafe("/api/v1/products?maxPrice=150")).GetProperty("total").GetInt32());

        db.PriceSlabs.Add(Slab(none, 10, 300m)); await db.SaveChangesAsync();                          // the third product gets a tier and appears
        Assert.Equal(3, (await client.GetFromJsonAsyncSafe("/api/v1/products")).GetProperty("total").GetInt32());
    }

    [MySqlFact]
    public Task On_mysql_the_columns_equal_the_minimum_and_maximum_of_the_tiers_after_seeding_and_after_an_edit() => MySqlStore.RunAsync(async () =>
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await CatalogueSeeder.SeedAsync(db, 300, sellers: 5);

        async Task<int> MismatchesAsync()
        {
            await using var fresh = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseMySQL(MySqlStore.CurrentConnectionString!).Options);
            var products = await fresh.Products.AsNoTracking().Select(p => new { p.PriceFrom, p.PriceTo, Min = fresh.PriceSlabs.Where(s => s.ProductId == p.Id).Min(s => (decimal?)s.PricePerUnit), Max = fresh.PriceSlabs.Where(s => s.ProductId == p.Id).Max(s => (decimal?)s.PricePerUnit) }).ToListAsync();
            Assert.Equal(300, products.Count);
            return products.Count(x => x.PriceFrom != x.Min || x.PriceTo != x.Max);
        }
        Assert.Equal(0, await MismatchesAsync());

        await using (var edit = new MarketplaceDbContext(new DbContextOptionsBuilder<MarketplaceDbContext>().UseMySQL(MySqlStore.CurrentConnectionString!).Options))
        {
            foreach (var slab in await edit.PriceSlabs.Where(x => x.ProductId % 7 == 0).ToListAsync()) slab.PricePerUnit += 13.37m;
            edit.PriceSlabs.RemoveRange(await edit.PriceSlabs.Where(x => x.ProductId % 11 == 0).ToListAsync());
            await edit.SaveChangesAsync();
        }
        Assert.Equal(0, await MismatchesAsync());
    });
}
