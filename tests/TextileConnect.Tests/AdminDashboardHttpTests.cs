using TextileConnect.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class AdminDashboardHttpTests
{
    [Fact]
    public async Task Admin_home_reports_today_week_month_and_decision_countsAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync(app => app.MapAdminDashboardEndpoints());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Orders.RemoveRange(db.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            db.OrderItems.RemoveRange(db.OrderItems.Where(x => x.SellerOrder!.Order!.OrderNumber == "PAID-POPULARITY"));   // a real database refuses to delete a parent that still has lines
            db.SellerOrders.RemoveRange(db.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            await db.SaveChangesAsync();
        }
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        using var admin = await fixture.ClientAsync("Admin", "admin-user");

        await SellerOrderHttpTests.PlaceAsync(retailer);                        // about 14 days ago: only in the 30 day figures
        fixture.Clock.Advance(TimeSpan.FromDays(10));
        await SellerOrderHttpTests.PlaceAsync(retailer);                        // about 4 days ago: in 7 and 30 days
        fixture.Clock.Advance(TimeSpan.FromDays(4));
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);         // today, but rejected and refunded: not revenue
        var today = await SellerOrderHttpTests.PlaceAsync(retailer);            // today
        await SellerOrderHttpTests.PlaceAsync(retailer, pay: false);            // never paid: not counted
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var now = fixture.Clock.GetUtcNow().UtcDateTime;
            db.Businesses.Add(new Business { Id = 77, BusinessType = BusinessType.Retailer, LegalName = "New Shop", DisplayName = "New Shop", Phone = "+919000000077", CreatedAtUtc = now }); // Pending by default
            db.ReturnClaims.Add(new ReturnClaim { SellerOrderId = today.SellerOrderId, Reason = "Damaged" });
            db.Refunds.Add(new Refund { PaymentId = 1, SellerOrderId = today.SellerOrderId, Amount = 1, Reason = "Stuck", Status = RefundStatus.Requested });
            await db.SaveChangesAsync();
        }

        var board = await PaymentHttpTests.JsonAsync(await admin.GetAsync("/api/v1/admin/dashboard"));
        var day = board.GetProperty("today"); var week = board.GetProperty("last7Days"); var month = board.GetProperty("last30Days");
        Assert.Equal(1, day.GetProperty("orders").GetInt32());
        Assert.Equal(31440m, day.GetProperty("gmv").GetDecimal());
        Assert.Equal(576m, day.GetProperty("commission").GetDecimal());
        Assert.Equal(2, week.GetProperty("orders").GetInt32());
        Assert.Equal(31440m * 2, week.GetProperty("gmv").GetDecimal());
        Assert.Equal(3, month.GetProperty("orders").GetInt32());
        Assert.Equal(31440m * 3, month.GetProperty("gmv").GetDecimal());
        Assert.Equal(576m * 3, month.GetProperty("commission").GetDecimal());
        Assert.Equal(1, day.GetProperty("newBusinesses").GetInt32());
        Assert.True(month.GetProperty("newBusinesses").GetInt32() >= 1);
        var needs = board.GetProperty("needsDecision");
        Assert.Equal(2, needs.GetProperty("pendingApprovals").GetInt32()); // the seeded unapproved seller and the new shop
        Assert.Equal(1, needs.GetProperty("openClaims").GetInt32());
        Assert.Equal(1, needs.GetProperty("refundsWaiting").GetInt32());
        Assert.Equal(0, needs.GetProperty("pendingProducts").GetInt32());

        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/dashboard")).StatusCode);
    }
}
