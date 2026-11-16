using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class LandedCostHttpTests
{
    private static Task<MarketplaceHttpFixture> StartAsync()
        => MarketplaceHttpFixture.StartAsync(app => { app.MapCatalogueEndpoints(); app.MapLandedCostEndpoints(); });

    private static async Task SeedRatesAsync(MarketplaceHttpFixture fixture, ShippingRateType type, decimal rate, decimal min)
    {
        await CatalogueHttpTests.SeedAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var from = new DeliveryZone { Name = "Punjab", PincodePrefix = "14" };
        var to = new DeliveryZone { Name = "Central", PincodePrefix = "45" };
        db.DeliveryZones.AddRange(from, to);
        await db.SaveChangesAsync();
        db.DeliveryZoneRates.Add(new DeliveryZoneRate { FromZoneId = from.Id, ToZoneId = to.Id, RateType = type, Rate = rate, MinCharge = min, TransitDays = 4 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Per_piece_zone_rate_gives_server_computed_totalsAsync()
    {
        await using var fixture = await StartAsync();
        await SeedRatesAsync(fixture, ShippingRateType.PerPiece, 20m, 1000m);
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        var quote = await client.GetFromJsonAsync<JsonElement>("/api/v1/landed-cost?productId=1&qty=50&pincode=452001");
        Assert.Equal(24000m, quote.GetProperty("factoryTotal").GetDecimal());
        Assert.Equal(1200m, quote.GetProperty("gstTotal").GetDecimal());
        Assert.Equal(1000m, quote.GetProperty("shippingTotal").GetDecimal());
        Assert.Equal(26200m, quote.GetProperty("total").GetDecimal());
        Assert.Equal(524m, quote.GetProperty("landedPerUnit").GetDecimal());
        Assert.Equal(480m, quote.GetProperty("slabPrice").GetDecimal());
        Assert.Equal(0m, quote.GetProperty("platformFeePerUnit").GetDecimal());
        Assert.Equal("quoted", quote.GetProperty("shippingStatus").GetString());
        Assert.Equal(4, quote.GetProperty("transitDays").GetInt32());
        var tier = await client.GetFromJsonAsync<JsonElement>("/api/v1/landed-cost?productId=1&qty=500&pincode=452001");
        Assert.Equal(440m, tier.GetProperty("slabPrice").GetDecimal());
        Assert.Equal(10000m + 220000m + 11000m, tier.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Per_kg_rate_uses_unit_weight_and_minimum_chargeAsync()
    {
        await using var fixture = await StartAsync();
        await SeedRatesAsync(fixture, ShippingRateType.PerKg, 30m, 500m);
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        var quote = await client.GetFromJsonAsync<JsonElement>("/api/v1/landed-cost?productId=1&qty=50&pincode=452001");
        Assert.Equal(1200m, quote.GetProperty("shippingTotal").GetDecimal()); // 30 x 50 x 0.8 kg
        // Fleece hoodie has no weight, so a per-kg rate cannot produce a quote.
        var noWeight = await client.GetFromJsonAsync<JsonElement>("/api/v1/landed-cost?productId=2&qty=20&pincode=452001");
        Assert.Equal("on_request", noWeight.GetProperty("shippingStatus").GetString());
        Assert.Equal(JsonValueKind.Null, noWeight.GetProperty("total").ValueKind);
    }

    [Fact]
    public async Task Missing_rate_is_on_request_and_invalid_input_is_rejectedAsync()
    {
        await using var fixture = await StartAsync();
        await CatalogueHttpTests.SeedAsync(fixture);
        using var client = await fixture.ClientAsync("Retailer", businessId: 30);
        var quote = await client.GetFromJsonAsync<JsonElement>("/api/v1/landed-cost?productId=1&qty=50&pincode=452001");
        Assert.Equal("on_request", quote.GetProperty("shippingStatus").GetString());
        Assert.Equal(24000m, quote.GetProperty("factoryTotal").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/landed-cost?productId=1&qty=49&pincode=452001")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/landed-cost?productId=1&qty=0&pincode=452001")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/landed-cost?productId=1&qty=50&pincode=12")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/landed-cost?productId=1&qty=50")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/landed-cost?productId=1&qty=50&pincode=999999")).StatusCode);
        foreach (var id in new[] { 3, 4, 99 })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/landed-cost?productId={id}&qty=50&pincode=452001")).StatusCode);
    }

    [Fact]
    public async Task Landed_cost_is_retailer_onlyAsync()
    {
        await using var fixture = await StartAsync();
        await CatalogueHttpTests.SeedAsync(fixture);
        const string url = "/api/v1/landed-cost?productId=1&qty=50&pincode=452001";
        using (var anonymous = await fixture.ClientAsync(null))
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        using (var seller = await fixture.ClientAsync("Manufacturer", businessId: 10))
            Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Catalogue_cards_and_detail_include_landed_cost_for_a_pincodeAsync()
    {
        await using var fixture = await StartAsync();
        await SeedRatesAsync(fixture, ShippingRateType.PerPiece, 20m, 1000m);
        using var client = await fixture.ClientAsync(null);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=puffer&pincode=452001");
        Assert.Equal(26200m, list.GetProperty("items")[0].GetProperty("landedCost").GetProperty("total").GetDecimal());
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/v1/products/puffer?pincode=452001&qty=200");
        Assert.Equal(459m, detail.GetProperty("landedCost").GetProperty("slabPrice").GetDecimal());
        var plain = await client.GetFromJsonAsync<JsonElement>("/api/v1/products/puffer");
        Assert.True(plain.GetProperty("landedCost").ValueKind is JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/products/puffer?pincode=452001&qty=10")).StatusCode);
    }
}
