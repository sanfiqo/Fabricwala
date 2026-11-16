using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class CartHttpTests
{
    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapCartEndpoints());
        await CatalogueHttpTests.SeedAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.ProductVariants.AddRange(new ProductVariant { Id = 10, ProductId = 1, Sku = "PUFFER-XL", Size = "XL", Colour = "Black", StockQty = 1000 },
            new ProductVariant { Id = 11, ProductId = 4, Sku = "UNAPPROVED-M", Size = "M", Colour = "Red", StockQty = 100 },
            new ProductVariant { Id = 12, ProductId = 3, Sku = "DRAFT-M", Size = "M", Colour = "Red", StockQty = 100 });
        await db.SaveChangesAsync();
        return fixture;
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, long variantId, int quantity)
        => client.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity });

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Cart_prices_use_tier_gst_percentage_and_next_tier_savingsAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        var cart = await JsonAsync(await AddAsync(client, 1, 60));
        Assert.Equal(28800m, cart.GetProperty("subTotal").GetDecimal());
        Assert.Equal(1440m, cart.GetProperty("taxTotal").GetDecimal());
        Assert.Equal(30240m, cart.GetProperty("grandTotal").GetDecimal());
        Assert.True(cart.GetProperty("canCheckout").GetBoolean());
        var item = cart.GetProperty("groups")[0].GetProperty("items")[0];
        Assert.Equal(480m, item.GetProperty("unitPrice").GetDecimal());
        var next = item.GetProperty("nextTier");
        Assert.Equal(200, next.GetProperty("minQuantity").GetInt32());
        Assert.Equal(459m, next.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(140, next.GetProperty("addQuantity").GetInt32());
        Assert.Equal(21m, next.GetProperty("savingPerUnit").GetDecimal());
        Assert.Equal(4200m, next.GetProperty("savingTotal").GetDecimal());

        // Quantity of one product across sizes counts together for the tier.
        cart = await JsonAsync(await AddAsync(client, 10, 140));
        foreach (var line in cart.GetProperty("groups")[0].GetProperty("items").EnumerateArray())
        {
            Assert.Equal(200, line.GetProperty("productQuantity").GetInt32());
            Assert.Equal(459m, line.GetProperty("unitPrice").GetDecimal());
        }
        Assert.Equal(200 * 459m, cart.GetProperty("subTotal").GetDecimal());
        var top = await JsonAsync(await AddAsync(client, 1, 300));
        Assert.Equal(JsonValueKind.Null, top.GetProperty("groups")[0].GetProperty("items")[0].GetProperty("nextTier").ValueKind);
    }

    [Fact]
    public async Task Cart_flags_below_moq_and_update_remove_work_for_the_ownerAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        var cart = await JsonAsync(await AddAsync(client, 1, 10));
        Assert.False(cart.GetProperty("canCheckout").GetBoolean());
        var line = cart.GetProperty("groups")[0].GetProperty("items")[0];
        Assert.Equal("below_moq", line.GetProperty("issues")[0].GetString());
        var id = line.GetProperty("id").GetInt64();
        cart = await JsonAsync(await client.PutAsJsonAsync($"/api/v1/cart/items/{id}", new { variantId = 1, quantity = 50 }));
        Assert.True(cart.GetProperty("canCheckout").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/cart/items/{id}", new { variantId = 1, quantity = 1001 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/cart/items/{id}", new { variantId = 1, quantity = 0 })).StatusCode);
        cart = await JsonAsync(await client.DeleteAsync($"/api/v1/cart/items/{id}"));
        Assert.Empty(cart.GetProperty("groups").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/cart/items/{id}")).StatusCode);
    }

    [Fact]
    public async Task Add_rejects_invalid_quantity_stock_and_unpurchasable_variantsAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        foreach (var quantity in new[] { 0, -1, 1_000_001, 1001 })
            Assert.Equal(HttpStatusCode.BadRequest, (await AddAsync(client, 1, quantity)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AddAsync(client, 1, 900)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AddAsync(client, 1, 101)).StatusCode); // cumulative stock
        foreach (var variant in new long[] { 2, 11, 12, 999 }) // inactive, unapproved seller, draft, missing
            Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(client, variant, 50)).StatusCode);
    }

    [Fact]
    public async Task Cart_is_isolated_by_business_and_retailer_onlyAsync()
    {
        await using var fixture = await StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner", 30);
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        var id = (await JsonAsync(await AddAsync(owner, 1, 60))).GetProperty("groups")[0].GetProperty("items")[0].GetProperty("id").GetInt64();
        Assert.Empty((await JsonAsync(await other.GetAsync("/api/v1/cart"))).GetProperty("groups").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/cart/items/{id}", new { variantId = 1, quantity = 70 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/cart/items/{id}")).StatusCode);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller", 10);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/cart")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/cart")).StatusCode);
    }

    [Fact]
    public async Task Unavailable_items_are_flagged_and_excluded_from_totalsAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        await AddAsync(client, 3, 20);
        await AddAsync(client, 1, 50);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Products.Single(x => x.Id == 2).Status = ProductStatus.Draft;
            await db.SaveChangesAsync();
        }
        var cart = await JsonAsync(await client.GetAsync("/api/v1/cart"));
        Assert.False(cart.GetProperty("canCheckout").GetBoolean());
        Assert.Equal(50 * 480m, cart.GetProperty("subTotal").GetDecimal());
        var flagged = cart.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray())
            .Single(x => x.GetProperty("variantId").GetInt64() == 3);
        Assert.Equal("unavailable", flagged.GetProperty("issues")[0].GetString());
    }
}
