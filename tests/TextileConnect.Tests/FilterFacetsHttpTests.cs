using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TextileConnect.Api;
using TextileConnect.Application.Catalogue;

namespace TextileConnect.Tests;

public sealed class FilterFacetsHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Client)> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        await CatalogueHttpTests.SeedAsync(fixture); // product 1 "Quilted puffer jacket" (Polyester, L Black, price 440-480), product 2 "Fleece hoodie" (Fleece, M Red, price 300)
        return (fixture, await fixture.ClientAsync(null));
    }

    private static async Task<JsonElement> FiltersAsync(HttpClient client, string query = "")
        => await client.GetFromJsonAsync<JsonElement>("/api/v1/products/filters" + (query.Length == 0 ? "" : "?" + query));

    private static Dictionary<string, int> Counts(JsonElement facet, string key = "value")
        => facet.EnumerateArray().ToDictionary(x => x.GetProperty(key).GetString()!, x => x.GetProperty("count").GetInt32());

    [Fact]
    public async Task Without_filters_every_facet_counts_the_whole_catalogueAsync()
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;
        var filters = await FiltersAsync(client);

        Assert.Equal(2, filters.GetProperty("total").GetInt32());
        Assert.Equal(2, filters.GetProperty("categories").EnumerateArray().Single().GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["Polyester"] = 1, ["Fleece"] = 1 }, Counts(filters.GetProperty("materials")));
        Assert.Equal(new Dictionary<string, int> { ["L"] = 1, ["M"] = 1 }, Counts(filters.GetProperty("sizes")));       // the inactive XS is not offered
        Assert.Equal(new Dictionary<string, int> { ["Black"] = 1, ["Red"] = 1 }, Counts(filters.GetProperty("colours")));
        Assert.Equal(2, filters.GetProperty("cities").EnumerateArray().Single().GetProperty("count").GetInt32());
        Assert.Equal(2, filters.GetProperty("states").EnumerateArray().Single().GetProperty("count").GetInt32());
        Assert.Equal(300m, filters.GetProperty("price").GetProperty("min").GetDecimal());
        Assert.Equal(440m, filters.GetProperty("price").GetProperty("max").GetDecimal());
        Assert.Empty(filters.GetProperty("seasons").EnumerateArray());
    }

    [Fact]
    public async Task A_filter_narrows_the_other_facets_but_not_its_ownAsync()
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;

        var polyester = await FiltersAsync(client, "material=Polyester");
        Assert.Equal(1, polyester.GetProperty("total").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["Polyester"] = 1, ["Fleece"] = 1 }, Counts(polyester.GetProperty("materials"))); // the shopper can still switch to Fleece
        Assert.Equal(new Dictionary<string, int> { ["L"] = 1 }, Counts(polyester.GetProperty("sizes")));                           // sizes only of what is left
        Assert.Equal(new Dictionary<string, int> { ["Black"] = 1 }, Counts(polyester.GetProperty("colours")));

        var sizeM = await FiltersAsync(client, "size=M");
        Assert.Equal(1, sizeM.GetProperty("total").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["L"] = 1, ["M"] = 1 }, Counts(sizeM.GetProperty("sizes")));
        Assert.Equal(new Dictionary<string, int> { ["Fleece"] = 1 }, Counts(sizeM.GetProperty("materials")));

        var cheap = await FiltersAsync(client, "maxPrice=300");
        Assert.Equal(1, cheap.GetProperty("total").GetInt32());
        Assert.Equal(440m, cheap.GetProperty("price").GetProperty("max").GetDecimal()); // the price range itself ignores the price filter, so the slider still shows the full range
    }

    [Fact]
    public async Task The_text_search_and_the_list_agree_with_the_countsAsync()
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;
        var filters = await FiltersAsync(client, "q=jacket");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=jacket");
        Assert.Equal(list.GetProperty("total").GetInt32(), filters.GetProperty("total").GetInt32());
        Assert.Equal(1, filters.GetProperty("total").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["Polyester"] = 1 }, Counts(filters.GetProperty("materials")));

        var none = await FiltersAsync(client, "q=nothing-like-this");
        Assert.Equal(0, none.GetProperty("total").GetInt32());
        Assert.Empty(none.GetProperty("categories").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, none.GetProperty("price").GetProperty("min").ValueKind);
    }

    [Fact]
    public async Task Bad_parameters_are_refused_and_the_route_does_not_swallow_a_product_pageAsync()
    {
        var (fixture, client) = await StartAsync();
        await using var _ = fixture; using var __ = client;
        foreach (var bad in new[] { "minPrice=500&maxPrice=100", "maxMoq=0", "pincode=bad", "q=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/products/filters?" + bad)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/products/puffer")).StatusCode);

        // A seller who calls a product "Filters" gets another page name, so /products/filters stays the counts route.
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var created = await (await seller.PostAsJsonAsync("/api/v1/seller/products", new { title = "Filters", publish = false })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("filters-product", created.GetProperty("slug").GetString());
    }

    [Theory]
    [InlineData("puffer jacket", "+puffer* +jacket*")]
    [InlineData("  Jacket!  ", "+jacket*")]
    [InlineData("cotton-kurti", "+cotton* +kurti*")]
    [InlineData("ab", null)]                       // too short for the full-text index
    [InlineData("kurti xl", null)]                 // one short word sends the whole search the "contains" way
    [InlineData("the jacket", null)]               // "the" is a word the index ignores
    [InlineData("जैकेट", null)]                    // non-English letters use "contains"
    [InlineData("a b c d e f g h i", null)]
    [InlineData("one two three four five six seven eight nine", null)] // more than eight words
    [InlineData("jacket\" -(x) +*", null)]         // operator characters are dropped; "x" is short, so this one falls back
    [InlineData("jacket \" -(wool) +*", "+jacket* +wool*")] // operators never reach MySQL
    public void The_search_text_becomes_a_safe_full_text_query_or_falls_back(string text, string? expected)
        => Assert.Equal(expected, ProductTextSearch.For(text).BooleanQuery);

    [Fact]
    public void An_empty_search_matches_everything_and_the_like_text_is_kept_for_the_fallback()
    {
        Assert.Equal(new ProductTextSearch.Plan(null, null), ProductTextSearch.For("  "));
        Assert.Equal(new ProductTextSearch.Plan(null, null), ProductTextSearch.For(null));
        Assert.Equal("kurti xl", ProductTextSearch.For(" Kurti XL ").Like);
    }
}
