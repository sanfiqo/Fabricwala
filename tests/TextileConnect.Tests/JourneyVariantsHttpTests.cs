using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The two unhappy paths of the journey, again over real HTTP endpoints.</summary>
public sealed class JourneyVariantsHttpTests
{
    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await FullJourneyHttpTests.StartAsync();
        return fixture;
    }

    private static async Task RunJobAsync(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
    }

    [Fact]
    public async Task SellerDoesNotAcceptInTime_OrderIsCancelledAndRefundedAsync()
    {
        await using var fixture = await StartAsync();
        var (retailer, seller, _, _, sellerId, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, total) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "late-order-1", pay: true);

        fixture.Clock.Advance(TimeSpan.FromHours(23));
        await RunJobAsync(fixture);
        Assert.Equal("WaitingForSeller", (await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("status").GetString()); // still inside the window
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var late = await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("accept_window_expired", (await late.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await RunJobAsync(fixture);

        var detail = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
        Assert.Equal("problems", detail.GetProperty("group").GetString());
        Assert.Equal("Refunded", detail.GetProperty("paymentStatus").GetString());
        var done = (await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/orders?status=done")).GetProperty("items")[0];
        Assert.Equal("Cancelled", done.GetProperty("status").GetString());
        Assert.Equal("Cancelled", (await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/payouts")).GetProperty("payouts").GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Contains("Order cancelled", (await retailer.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("title").GetString()));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var refund = await db.Refunds.SingleAsync(x => x.SellerOrderId == sellerOrderId);
        Assert.Equal(total, refund.Amount); // the full amount paid, including GST and freight
        Assert.Equal("Processed", refund.Status);
        Assert.Equal(500, (await db.ProductVariants.SingleAsync(x => x.Id == variantId)).StockQty); // the reserved stock is back
        Assert.Equal(sellerId, (await db.SellerOrders.SingleAsync(x => x.Id == sellerOrderId)).ManufacturerBusinessId);
    }

    [Fact]
    public async Task RetailerReportsAProblem_PayoutStaysOnHoldAsync()
    {
        await using var fixture = await StartAsync();
        var (retailer, seller, admin, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, total) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "problem-order-1", pay: true);
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-1" });

        var report = await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", new { reason = "Damaged", pieces = 12, note = "Water damage on cartons" });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var claimId = (await report.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        fixture.Clock.Advance(TimeSpan.FromDays(30)); // well past the 7 day auto release
        await RunJobAsync(fixture);

        var held = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/payouts");
        Assert.Equal("OnHold", held.GetProperty("payouts").GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(total - 960m, held.GetProperty("onHoldAmount").GetDecimal());
        Assert.Equal(0m, held.GetProperty("paidThisMonth").GetDecimal());
        var tracking = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("ProblemReported", tracking.GetProperty("status").GetString());
        Assert.Equal("problems", tracking.GetProperty("group").GetString());

        // Once an admin rejects the claim the money flows again.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "Rejected", refundAmount = 0, note = "Photos show normal wear" })).StatusCode);
        await RunJobAsync(fixture);
        var released = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/payouts");
        Assert.Equal("Paid", released.GetProperty("payouts").GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(total - 960m, released.GetProperty("paidThisMonth").GetDecimal());
    }
}
