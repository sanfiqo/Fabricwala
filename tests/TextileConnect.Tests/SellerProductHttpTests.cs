using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class SellerProductHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, long CategoryId)> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => { app.MapProductEndpoints(); app.MapProductModerationEndpoints(); });
        await CatalogueHttpTests.SeedAsync(fixture); // seller 10 owns products 1 (active), 2 (active), 3 (draft)
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        return (fixture, (await db.Categories.SingleAsync(x => x.Slug == "winter-wear")).Id);
    }

    private static object Request(long categoryId, string title = "Cotton kurti set", bool publish = true, object? variants = null, object? tiers = null, int moq = 20)
        => new
        {
            title, categoryId, description = "Soft cotton", material = "Cotton", hsnCode = "6104", gstRate = 5, moq, unit = 0, weightPerUnitGrams = 300, dispatchDays = 3,
            variants = variants ?? new[] { new { size = "M", colour = "Blue", stockQty = 100 }, new { size = "L", colour = "Blue", stockQty = 3 } },
            tiers = tiers ?? new[] { new { minQty = 20, pricePerUnit = 300m }, new { minQty = 100, pricePerUnit = 280m } },
            publish
        };

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string[]> FieldsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        return body.GetProperty("fields").EnumerateObject().Select(x => x.Name).OrderBy(x => x).ToArray();
    }

    [Fact]
    public async Task Draft_saves_with_only_a_title_and_publishing_applies_every_ruleAsync()
    {
        var (fixture, categoryId) = await StartAsync();
        await using var _ = fixture;
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);

        var draft = await BodyAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", new { title = "Wool shawl", publish = false }), HttpStatusCode.Created);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.False(draft.GetProperty("isActive").GetBoolean());
        Assert.Equal("wool-shawl", draft.GetProperty("slug").GetString());
        var second = await BodyAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", new { title = "Wool Shawl!", publish = false }), HttpStatusCode.Created);
        Assert.Equal("wool-shawl-2", second.GetProperty("slug").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync("/api/v1/products/wool-shawl")).StatusCode); // drafts are not public

        Assert.Equal(new[] { "title" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", new { title = "  ", publish = false })));
        Assert.Equal(new[] { "categoryId", "moq", "tiers", "variants" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", new { title = "Empty", publish = true })));
        Assert.Equal(new[] { "tiers[0].minQty" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId, tiers: new[] { new { minQty = 30, pricePerUnit = 300m } }))));
        Assert.Equal(new[] { "tiers[1].pricePerUnit" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products",
            Request(categoryId, tiers: new[] { new { minQty = 20, pricePerUnit = 300m }, new { minQty = 50, pricePerUnit = 310m } }))));
        Assert.Equal(new[] { "tiers[0].pricePerUnit" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId, tiers: new[] { new { minQty = 20, pricePerUnit = 300.555m } }))));
        Assert.Equal(new[] { "variants[1]" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products",
            Request(categoryId, variants: new[] { new { size = "M", colour = "Blue", stockQty = 5 }, new { size = "m", colour = "blue", stockQty = 5 } }))));
        Assert.Equal(new[] { "categoryId" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(999_999))));
        Assert.Equal(new[] { "variants[0].stockQty" }, await FieldsAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId, variants: new[] { new { size = "M", colour = "Blue", stockQty = -1 } }))));

        var published = await BodyAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId)), HttpStatusCode.Created);
        Assert.Equal("Active", published.GetProperty("status").GetString());
        Assert.True(published.GetProperty("isActive").GetBoolean());
        var tiers = published.GetProperty("tiers").EnumerateArray().ToList();
        Assert.Equal(99, tiers[0].GetProperty("maxQty").GetInt32());
        Assert.Equal(JsonValueKind.Null, tiers[1].GetProperty("maxQty").ValueKind);
        var skus = published.GetProperty("variants").EnumerateArray().Select(x => x.GetProperty("sku").GetString()).ToList();
        Assert.Equal(2, skus.Distinct().Count());
        Assert.All(skus, sku => Assert.StartsWith("COTTONKURTIS-", sku));
        var publicView = await BodyAsync(await seller.GetAsync("/api/v1/products/cotton-kurti-set"));
        Assert.Equal(280m, publicView.GetProperty("priceFrom").GetDecimal());
        Assert.Equal(300m, publicView.GetProperty("priceTo").GetDecimal());
    }

    [Fact]
    public async Task Edit_replaces_tiers_keeps_variants_referenced_and_checks_ownershipAsync()
    {
        var (fixture, categoryId) = await StartAsync();
        await using var _ = fixture;
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var created = await BodyAsync(await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId)), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt64();
        var variants = created.GetProperty("variants").EnumerateArray().ToList();
        var keepId = variants[0].GetProperty("id").GetInt64();

        var edit = Request(categoryId, title: "Cotton kurti set v2", moq: 10,
            variants: new object[] { new { id = keepId, size = "M", colour = "Blue", stockQty = 250, isActive = true }, new { size = "XL", colour = "Red", stockQty = 40 } },
            tiers: new[] { new { minQty = 10, pricePerUnit = 320m }, new { minQty = 200, pricePerUnit = 290m } });
        var updated = await BodyAsync(await seller.PutAsJsonAsync($"/api/v1/seller/products/{id}", edit));
        Assert.Equal("Cotton kurti set v2", updated.GetProperty("title").GetString());
        Assert.Equal(created.GetProperty("slug").GetString(), updated.GetProperty("slug").GetString()); // links stay stable
        var after = updated.GetProperty("variants").EnumerateArray().ToList();
        Assert.Equal(3, after.Count);
        Assert.Equal(250, after.Single(x => x.GetProperty("id").GetInt64() == keepId).GetProperty("stockQty").GetInt32());
        var dropped = after.Single(x => x.GetProperty("id").GetInt64() == variants[1].GetProperty("id").GetInt64());
        Assert.False(dropped.GetProperty("isActive").GetBoolean()); // not deleted: carts and orders may still point at it
        Assert.Equal(new[] { 10, 200 }, updated.GetProperty("tiers").EnumerateArray().Select(x => x.GetProperty("minQty").GetInt32()).ToArray());
        Assert.Equal(2, await CountAsync(fixture, db => db.PriceSlabs.Where(x => x.ProductId == id)));

        Assert.Equal(new[] { "variants[0].id" }, await FieldsAsync(await seller.PutAsJsonAsync($"/api/v1/seller/products/{id}",
            Request(categoryId, moq: 10, variants: new object[] { new { id = 99999, size = "M", colour = "Blue", stockQty = 1 } }, tiers: new[] { new { minQty = 10, pricePerUnit = 320m } }))));
        var taken = after[0].GetProperty("sku").GetString();
        Assert.Equal(new[] { "variants[0].sku" }, await FieldsAsync(await seller.PutAsJsonAsync($"/api/v1/seller/products/{id}",
            Request(categoryId, moq: 10, variants: new object[] { new { size = "S", colour = "Green", sku = taken, stockQty = 1 } }, tiers: new[] { new { minQty = 10, pricePerUnit = 320m } }))));
        Assert.Equal(new[] { "tiers" }, await FieldsAsync(await seller.PutAsJsonAsync($"/api/v1/seller/products/{id}", Request(categoryId, moq: 10, tiers: Array.Empty<object>()))));

        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 11);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/seller/products/{id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/seller/products/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/seller/products/{id}/active", new { active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/seller/products/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.PutAsJsonAsync("/api/v1/seller/products/99999", edit)).StatusCode);
    }

    [Fact]
    public async Task Toggle_switches_visibility_and_an_admin_hold_cannot_be_overriddenAsync()
    {
        var (fixture, categoryId) = await StartAsync();
        await using var _ = fixture;
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        using var admin = await fixture.ClientAsync("Admin", "admin-user");
        var off = await BodyAsync(await seller.PutAsJsonAsync("/api/v1/seller/products/1/active", new { active = false }));
        Assert.Equal("Inactive", off.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync("/api/v1/products/puffer")).StatusCode);
        Assert.Equal("Active", (await BodyAsync(await seller.PutAsJsonAsync("/api/v1/seller/products/1/active", new { active = true }))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync("/api/v1/products/puffer")).StatusCode);
        Assert.Equal("Active", (await BodyAsync(await seller.PutAsJsonAsync("/api/v1/seller/products/1/active", new { active = true }))).GetProperty("status").GetString()); // repeat is harmless

        // The seeded draft has no tiers or variants, so it cannot be switched on.
        Assert.Equal(new[] { "tiers", "variants" }, await FieldsAsync(await seller.PutAsJsonAsync("/api/v1/seller/products/3/active", new { active = true })));
        Assert.Equal("Draft", (await BodyAsync(await seller.PutAsJsonAsync("/api/v1/seller/products/3/active", new { active = false }))).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/products/1/moderate", new { action = "Deactivate", reason = "Policy" })).StatusCode);
        var held = await seller.PutAsJsonAsync("/api/v1/seller/products/1/active", new { active = true });
        Assert.Equal(HttpStatusCode.Conflict, held.StatusCode);
        Assert.Equal("product_deactivated_by_admin", (await held.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var detail = await BodyAsync(await seller.GetAsync("/api/v1/seller/products/1"));
        Assert.True(detail.GetProperty("adminDeactivated").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await seller.PutAsJsonAsync("/api/v1/seller/products/1", Request(categoryId, publish: true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/products/1/moderate", new { action = "Activate", reason = "Cleared" })).StatusCode);
        Assert.Equal("Active", (await BodyAsync(await seller.GetAsync("/api/v1/seller/products/1"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task List_filters_search_and_paging_are_stable_and_isolatedAsync()
    {
        var (fixture, categoryId) = await StartAsync();
        await using var _ = fixture;
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5)); // later than the seeded rows, so "last updated first" is predictable
        await seller.PostAsJsonAsync("/api/v1/seller/products", Request(categoryId)); // stock 100 and 3 -> low stock at the default threshold 5
        await seller.PutAsJsonAsync("/api/v1/seller/products/2/active", new { active = false });

        async Task<JsonElement> List(string query) => await BodyAsync(await seller.GetAsync("/api/v1/seller/products" + query));
        string[] Slugs(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("slug").GetString()!).ToArray();
        var all = await List("");
        Assert.Equal(4, all.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "cotton-kurti-set", "fleece-hoodie", "draft", "puffer" }, Slugs(all)); // last updated first, then newest id
        Assert.Equal(new[] { "draft" }, Slugs(await List("?filter=draft")));
        Assert.Equal(new[] { "cotton-kurti-set", "puffer" }, Slugs(await List("?filter=active")).OrderBy(x => x).ToArray());
        Assert.Equal(new[] { "fleece-hoodie" }, Slugs(await List("?filter=inactive")));
        Assert.Equal(new[] { "cotton-kurti-set" }, Slugs(await List("?filter=low_stock")));
        Assert.Equal(new[] { "cotton-kurti-set", "fleece-hoodie", "puffer" }, Slugs(await List("?filter=low_stock&threshold=1000")).OrderBy(x => x).ToArray()); // drafts never count
        Assert.Equal(new[] { "puffer" }, Slugs(await List("?q=PUFFER")));
        Assert.Equal(new[] { "cotton-kurti-set" }, Slugs(await List("?q=COTTONKURTIS-M")));
        Assert.Empty(Slugs(await List("?q=nothing-like-this")));

        var first = await List("?pageSize=3&page=1");
        var last = await List("?pageSize=3&page=2");
        Assert.Equal(3, Slugs(first).Length);
        Assert.Equal(new[] { "puffer" }, Slugs(last));
        Assert.Equal(4, last.GetProperty("total").GetInt32());
        var card = all.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "puffer");
        Assert.Equal(440m, card.GetProperty("priceFrom").GetDecimal());
        Assert.Equal(480m, card.GetProperty("priceTo").GetDecimal());
        Assert.Equal(1000, card.GetProperty("totalStock").GetInt32()); // the inactive variant is not counted
        foreach (var bad in new[] { "?filter=nope", "?page=0", "?pageSize=101", "?threshold=-1", "?threshold=1001" })
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.GetAsync("/api/v1/seller/products" + bad)).StatusCode);

        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 11);
        Assert.Equal(0, (await BodyAsync(await other.GetAsync("/api/v1/seller/products"))).GetProperty("total").GetInt32());
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/seller/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.PostAsJsonAsync("/api/v1/seller/products", new { title = "x" })).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await seller.DeleteAsync("/api/v1/seller/products/3")).StatusCode);
        Assert.Equal(3, (await List("")).GetProperty("total").GetInt32());
    }

    private static async Task<int> CountAsync<T>(MarketplaceHttpFixture fixture, Func<MarketplaceDbContext, IQueryable<T>> query)
    {
        using var scope = fixture.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>()).CountAsync();
    }
}
