using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Popularity is a stored counter that goes up when the retailer confirms delivery, not a sum over order items on every search.</summary>
public sealed class UnitsSoldCounterTests
{
    private static async Task<int> UnitsSoldAsync(MarketplaceHttpFixture fixture, long variantId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var productId = (await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == variantId)).ProductId;
        return (await db.Products.AsNoTracking().SingleAsync(x => x.Id == productId)).UnitsSold;
    }

    private static async Task RunJobAsync(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TextileConnect.Application.Orders.IOrderMaintenance>().ExpireAsync();
    }

    [Fact]
    public async Task Delivery_adds_the_pieces_once_and_the_search_shows_themAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, _, _, _, variantId, slug, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "units-delivery-1", pay: true);

        Assert.Equal(0, await UnitsSoldAsync(fixture, variantId)); // paid, not yet delivered
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-1" });
        Assert.Equal(0, await UnitsSoldAsync(fixture, variantId)); // shipped, not yet delivered

        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null)).StatusCode);
        Assert.Equal(100, await UnitsSoldAsync(fixture, variantId));
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null)).StatusCode); // pressing the button again
        Assert.Equal(100, await UnitsSoldAsync(fixture, variantId));

        var list = await retailer.GetFromJsonAsync<JsonElement>("/api/v1/products?sort=popular");
        Assert.Equal(100, list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == slug).GetProperty("unitsSold").GetInt32());
    }

    [Fact]
    public async Task Rejected_and_expired_orders_never_count_even_though_they_were_paidAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, _, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);

        var (_, rejectedId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "units-reject", pay: true);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejectedId}/reject", new { reason = "Out of stock" })).StatusCode);

        await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "units-expire", pay: true);
        fixture.Clock.Advance(TimeSpan.FromHours(30)); // past the 24 hour accept window
        await RunJobAsync(fixture);

        Assert.Equal(0, await UnitsSoldAsync(fixture, variantId));
    }

    [Fact]
    public async Task A_problem_report_does_not_take_pieces_back_off_the_counterAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, admin, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "units-claim", pay: true);
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-2" });
        var claimId = (await (await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", new { reason = "Damaged", pieces = 20, note = "Wet" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "PartiallyRefunded", refundAmount = 2000m, note = "20 pieces wet" })).StatusCode);

        // The goods were delivered and the retailer got money back for some of them; the rule is that delivered pieces stay counted (docs/SEARCH.md).
        Assert.Equal(100, await UnitsSoldAsync(fixture, variantId));
    }
}
