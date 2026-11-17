using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using TextileConnect.Api;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The word search through the MySQL full-text index. It cannot run on the in-memory store, so it needs TEXTILECONNECT_TEST_MYSQL (docs/TESTING.md).</summary>
public sealed class ProductSearchMySqlTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Client)> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        await CatalogueHttpTests.SeedAsync(fixture); // "Quilted puffer jacket" (Polyester) and "Fleece hoodie" (Fleece)
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var category = await db.Categories.FirstAsync();
            var extra = new[]
            {
                ("Cotton kurti set", "cotton-kurti-set", "Soft cotton kurti for summer", "Cotton"),
                ("Woollen shawl", "woollen-shawl", "Hand woven Kashmiri wool", "Wool"),
                ("XS tee", "xs-tee", "Tiny test shirt", "Cotton")
            };
            foreach (var (title, slug, description, material) in extra)
            {
                var product = new Product { SellerBusinessId = 10, CategoryId = category.Id, Title = title, Slug = slug, Description = description, Material = material, Moq = 10, Status = ProductStatus.Active };
                db.Products.Add(product);
                await db.SaveChangesAsync();
                db.PriceSlabs.Add(new PriceSlab { ProductId = product.Id, MinQty = 10, PricePerUnit = 100 });
            }
            await db.SaveChangesAsync();
        }
        return (fixture, await fixture.ClientAsync(null));
    }

    private static async Task<string[]> SlugsAsync(HttpClient client, string q)
        => (await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=" + Uri.EscapeDataString(q))).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("slug").GetString()!).OrderBy(x => x).ToArray();

    [MySqlFact]
    public Task The_full_text_index_exists_on_the_three_search_columns() => MySqlStore.RunAsync(async () =>
    {
        await using var connection = new MySqlConnection(MySqlStore.CurrentConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND INDEX_NAME = 'FTX_Products_Search' AND INDEX_TYPE = 'FULLTEXT'", connection);
        Assert.Equal(3L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    });

    [MySqlFact]
    public Task Words_match_the_start_of_a_word_in_the_title_the_description_or_the_material() => MySqlStore.RunAsync(async () =>
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;

        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "jacket"));
        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "jack"));                       // the start of a word
        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "JACKET"));                     // any case
        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "quilted jacket"));              // every word must be there
        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "jacket quilted"));              // in any order
        Assert.Empty(await SlugsAsync(client, "jacket shawl"));
        Assert.Equal(new[] { "puffer" }, await SlugsAsync(client, "polyester"));                   // the material
        Assert.Equal(new[] { "cotton-kurti-set", "xs-tee" }, await SlugsAsync(client, "cotton"));  // title, description or material
        Assert.Equal(new[] { "woollen-shawl" }, await SlugsAsync(client, "kashmiri"));              // the description
    });

    [MySqlFact]
    public Task Short_words_and_ignored_words_fall_back_to_contains_and_odd_characters_do_no_harm() => MySqlStore.RunAsync(async () =>
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;

        Assert.Equal(new[] { "xs-tee" }, await SlugsAsync(client, "xs"));                          // two letters: not in the index, found by "contains"
        Assert.Empty(await SlugsAsync(client, "kurti xs"));                                        // one short word sends the whole phrase the "contains" way: no product has that exact phrase
        Assert.NotNull(await SlugsAsync(client, "the"));                                           // an ignored word must not break the search
        foreach (var odd in new[] { "jacket\"(", "jacket +* -", "o'neil", "'; DROP TABLE Products; --", "jacket) AGAINST (", "%", "_" })
            Assert.NotNull(await SlugsAsync(client, odd));                                         // no SQL error, no 500
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(7, await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Products.CountAsync()); // nothing was dropped: 4 seeded products and 3 added here
    });

    [MySqlFact]
    public Task Filters_and_facets_work_on_top_of_the_text_search() => MySqlStore.RunAsync(async () =>
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=cotton&material=Cotton&sort=price-asc");
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        var facets = await client.GetFromJsonAsync<JsonElement>("/api/v1/products/filters?q=cotton");
        Assert.Equal(list.GetProperty("total").GetInt32(), facets.GetProperty("total").GetInt32());
        Assert.Equal(2, facets.GetProperty("materials").EnumerateArray().Single(x => x.GetProperty("value").GetString() == "Cotton").GetProperty("count").GetInt32());
    });
}
