using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class PayoutHttpTests
{
    private const decimal YouGet = 31440m - 576m; // order total minus the 2% platform fee on the goods

    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Retailer, HttpClient Seller)> StartAsync()
    {
        var fixture = await PaymentHttpTests.StartAsync(app => app.MapPayoutEndpoints());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Orders.RemoveRange(db.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            db.OrderItems.RemoveRange(db.OrderItems.Where(x => x.SellerOrder!.Order!.OrderNumber == "PAID-POPULARITY"));   // a real database refuses to delete a parent that still has lines
            db.SellerOrders.RemoveRange(db.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            await db.SaveChangesAsync();
        }
        return (fixture, await fixture.ClientAsync("Retailer", "retailer-user", 30), await fixture.ClientAsync("Manufacturer", "seller-user", 10));
    }

    private static async Task RunJobAsync(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
    }

    private static async Task<JsonElement> PayoutsAsync(HttpClient seller, string query = "")
        => await PaymentHttpTests.JsonAsync(await seller.GetAsync("/api/v1/seller/payouts" + query));

    private static JsonElement Row(JsonElement summary, int index = 0) => summary.GetProperty("payouts").GetProperty("items")[index];

    private static async Task ShipAsync(HttpClient seller, long sellerOrderId)
    {
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-500" });
    }

    [Fact]
    public async Task Payout_is_held_then_scheduled_on_dispatch_and_released_after_seven_daysAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (_, sellerOrderId) = await SellerOrderHttpTests.PlaceAsync(retailer);

        var held = await PayoutsAsync(seller);
        Assert.Equal(YouGet, held.GetProperty("onHoldAmount").GetDecimal());
        Assert.Equal(0m, held.GetProperty("nextPayoutAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, held.GetProperty("nextPayoutDate").ValueKind);
        var row = Row(held);
        Assert.Equal("OnHold", row.GetProperty("status").GetString());
        Assert.Equal(31440m, row.GetProperty("orderValue").GetDecimal());
        Assert.Equal(576m, row.GetProperty("platformFee").GetDecimal());
        Assert.Equal(YouGet, row.GetProperty("amount").GetDecimal());

        await ShipAsync(seller, sellerOrderId);
        var scheduled = await PayoutsAsync(seller);
        Assert.Equal("Scheduled", Row(scheduled).GetProperty("status").GetString());
        Assert.Equal(YouGet, scheduled.GetProperty("nextPayoutAmount").GetDecimal());
        Assert.Equal(0m, scheduled.GetProperty("onHoldAmount").GetDecimal());

        fixture.Clock.Advance(TimeSpan.FromDays(6));
        await RunJobAsync(fixture);
        Assert.Equal("Scheduled", Row(await PayoutsAsync(seller)).GetProperty("status").GetString()); // day 6: still waiting
        fixture.Clock.Advance(TimeSpan.FromDays(1.1));
        await RunJobAsync(fixture);
        await RunJobAsync(fixture); // repeating changes nothing
        var paid = await PayoutsAsync(seller);
        Assert.Equal("Paid", Row(paid).GetProperty("status").GetString());
        Assert.Equal(YouGet, paid.GetProperty("paidThisMonth").GetDecimal());
        Assert.Equal(0m, paid.GetProperty("nextPayoutAmount").GetDecimal());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "seller-user" && x.Title == "Payout released"));
    }

    [Fact]
    public async Task Confirmed_delivery_releases_the_payout_at_the_next_runAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (orderId, sellerOrderId) = await SellerOrderHttpTests.PlaceAsync(retailer);
        await ShipAsync(seller, sellerOrderId);
        await RunJobAsync(fixture);
        Assert.Equal("Scheduled", Row(await PayoutsAsync(seller)).GetProperty("status").GetString());
        await PaymentHttpTests.JsonAsync(await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null));
        await RunJobAsync(fixture);
        Assert.Equal("Paid", Row(await PayoutsAsync(seller)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Open_claim_keeps_the_payout_on_hold_until_resolvedAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (orderId, sellerOrderId) = await SellerOrderHttpTests.PlaceAsync(retailer);
        await ShipAsync(seller, sellerOrderId);
        await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.ReturnClaims.Add(new ReturnClaim { SellerOrderId = sellerOrderId, Reason = "Damaged" });
            await db.SaveChangesAsync();
        }
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        await RunJobAsync(fixture);
        var held = await PayoutsAsync(seller);
        Assert.Equal("OnHold", Row(held).GetProperty("status").GetString());
        Assert.Equal(YouGet, held.GetProperty("onHoldAmount").GetDecimal());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            (await db.ReturnClaims.SingleAsync()).ResolvedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        await RunJobAsync(fixture);
        Assert.Equal("Paid", Row(await PayoutsAsync(seller)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Rejected_order_has_no_payout_and_lists_filter_page_and_exportAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);
        var shipped = await SellerOrderHttpTests.PlaceAsync(retailer);
        await SellerOrderHttpTests.PlaceAsync(retailer); // stays waiting for the seller
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });
        await ShipAsync(seller, shipped.SellerOrderId);

        var all = await PayoutsAsync(seller);
        Assert.Equal(3, all.GetProperty("payouts").GetProperty("total").GetInt32());
        Assert.Equal(YouGet, all.GetProperty("onHoldAmount").GetDecimal()); // the waiting order only
        Assert.Equal(YouGet, all.GetProperty("nextPayoutAmount").GetDecimal()); // the shipped order only
        string[] Statuses(JsonElement s) => s.GetProperty("payouts").GetProperty("items").EnumerateArray().Select(x => x.GetProperty("status").GetString()!).ToArray();
        Assert.Equal(new[] { "OnHold", "Scheduled", "Cancelled" }, Statuses(all)); // newest first
        Assert.Equal(new[] { "OnHold" }, Statuses(await PayoutsAsync(seller, "?status=on_hold")));
        Assert.Equal(new[] { "Scheduled" }, Statuses(await PayoutsAsync(seller, "?status=scheduled")));
        Assert.Empty(Statuses(await PayoutsAsync(seller, "?status=paid")));
        Assert.Single(Statuses(await PayoutsAsync(seller, "?pageSize=1&page=2")));

        var response = await seller.GetAsync("/api/v1/seller/payouts?csv=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.MediaType);
        var lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("orderNumber,orderValue,platformFee,youGet,status,releaseDate,paidAt", lines[0]);
        Assert.Equal(4, lines.Length);
        Assert.Contains(",31440.00,576.00,30864.00,Scheduled,", lines[2]);
        Assert.DoesNotContain("account", string.Join(' ', lines), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Payouts_are_isolated_seller_only_and_validate_queryAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        await SellerOrderHttpTests.PlaceAsync(retailer);
        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 12);
        var theirs = await PayoutsAsync(other);
        Assert.Equal(0, theirs.GetProperty("payouts").GetProperty("total").GetInt32());
        Assert.Equal(0m, theirs.GetProperty("onHoldAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/seller/payouts")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/payouts")).StatusCode);
        foreach (var bad in new[] { "?status=nope", "?page=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.GetAsync("/api/v1/seller/payouts" + bad)).StatusCode);
    }
}
