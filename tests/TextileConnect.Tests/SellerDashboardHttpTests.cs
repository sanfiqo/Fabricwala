using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class SellerDashboardHttpTests
{
    private static async Task<JsonElement> DashboardAsync(HttpClient client)
        => await PaymentHttpTests.JsonAsync(await client.GetAsync("/api/v1/seller/dashboard"));

    [Fact]
    public async Task Dashboard_counts_sales_chart_low_stock_best_sellers_and_payout_from_real_ordersAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync(app => app.MapSellerDashboardEndpoints());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Orders.RemoveRange(db.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            db.OrderItems.RemoveRange(db.OrderItems.Where(x => x.SellerOrder!.Order!.OrderNumber == "PAID-POPULARITY"));   // a real database refuses to delete a parent that still has lines
            db.SellerOrders.RemoveRange(db.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            (await db.ProductVariants.SingleAsync(x => x.Id == 3)).StockQty = 2; // fleece hoodie is nearly out
            await db.SaveChangesAsync();
        }
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var fresh = await SellerOrderHttpTests.PlaceAsync(retailer);
        var toPack = await SellerOrderHttpTests.PlaceAsync(retailer);
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);
        await SellerOrderHttpTests.PlaceAsync(retailer, pay: false);
        await seller.PostAsync($"/api/v1/seller/orders/{toPack.SellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });

        var board = await DashboardAsync(seller);
        Assert.Equal(1, board.GetProperty("newOrders").GetInt32());
        Assert.Equal(1, board.GetProperty("toPack").GetInt32());
        Assert.Equal(57600m, board.GetProperty("salesThisMonth").GetDecimal()); // two live paid orders x 28,800 goods value
        Assert.Equal(30864m * 2, board.GetProperty("onHoldAmount").GetDecimal());
        Assert.Equal(0m, board.GetProperty("nextPayoutAmount").GetDecimal());
        var chart = board.GetProperty("monthlySales").EnumerateArray().ToList();
        Assert.Equal(6, chart.Count);
        Assert.Equal(57600m, chart[5].GetProperty("sales").GetDecimal());
        Assert.Equal(2, chart[5].GetProperty("orders").GetInt32());
        Assert.All(chart.Take(5), m => Assert.Equal(0m, m.GetProperty("sales").GetDecimal()));
        var low = board.GetProperty("lowStock");
        Assert.Equal(5, low.GetProperty("threshold").GetInt32());
        Assert.Equal(1, low.GetProperty("count").GetInt32());
        Assert.Equal("Fleece hoodie", low.GetProperty("items")[0].GetProperty("title").GetString());
        var best = board.GetProperty("bestSellers")[0];
        Assert.Equal("Quilted puffer jacket", best.GetProperty("title").GetString());
        Assert.Equal(120, best.GetProperty("quantity").GetInt32());
        Assert.Equal(57600m, best.GetProperty("revenue").GetDecimal());

        fixture.Clock.Advance(TimeSpan.FromDays(35)); // always lands in a later month
        await SellerOrderHttpTests.PlaceAsync(retailer);
        var later = (await DashboardAsync(seller)).GetProperty("monthlySales").EnumerateArray().ToList();
        Assert.Equal(28800m, later[5].GetProperty("sales").GetDecimal());
        Assert.Equal(57600m, later[4].GetProperty("sales").GetDecimal());

        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 12);
        var empty = await DashboardAsync(other);
        Assert.Equal(0, empty.GetProperty("newOrders").GetInt32());
        Assert.Equal(0m, empty.GetProperty("salesThisMonth").GetDecimal());
        Assert.Empty(empty.GetProperty("bestSellers").EnumerateArray());
        Assert.Equal(0, empty.GetProperty("lowStock").GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/seller/dashboard")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/dashboard")).StatusCode);
    }
}
