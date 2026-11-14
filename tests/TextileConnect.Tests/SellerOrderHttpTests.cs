using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Application.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class SellerOrderHttpTests
{
    private const long RetailerId = 30;
    private static int keyCounter;

    internal static async Task<(long OrderId, long SellerOrderId)> PlaceAsync(HttpClient retailer, bool pay = true)
    {
        var order = await PaymentHttpTests.PlaceOrderAsync(retailer);
        var orderId = order.GetProperty("id").GetInt64();
        if (pay)
        {
            var key = "seller-pay-key-" + Interlocked.Increment(ref keyCounter);
            var gatewayOrderId = (await PaymentHttpTests.JsonAsync(await PaymentHttpTests.StartPaymentAsync(retailer, orderId, key))).GetProperty("gatewayOrderId").GetString();
            await PaymentHttpTests.JsonAsync(await retailer.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null));
        }
        return (orderId, order.GetProperty("sellerOrders")[0].GetProperty("id").GetInt64());
    }

    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Retailer, HttpClient Seller)> StartAsync()
    {
        var fixture = await PaymentHttpTests.StartAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Orders.RemoveRange(db.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            db.SellerOrders.RemoveRange(db.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            await db.SaveChangesAsync();
        }
        return (fixture, await fixture.ClientAsync("Retailer", "retailer-user", RetailerId), await fixture.ClientAsync("Manufacturer", "seller-user", 10));
    }

    private static string Status(JsonElement order) => order.GetProperty("status").GetString()!;

    [Fact]
    public async Task Seller_tabs_show_only_paid_orders_with_fee_and_earningsAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var unpaid = await PlaceAsync(retailer, pay: false);
        var fresh = await PlaceAsync(retailer);
        var toPack = await PlaceAsync(retailer);
        var shipped = await PlaceAsync(retailer);
        var done = await PlaceAsync(retailer);
        await seller.PostAsync($"/api/v1/seller/orders/{toPack.SellerOrderId}/accept", null);
        foreach (var o in new[] { shipped, done })
        {
            await seller.PostAsync($"/api/v1/seller/orders/{o.SellerOrderId}/accept", null);
            await seller.PostAsJsonAsync($"/api/v1/seller/orders/{o.SellerOrderId}/dispatch", new { lrNumber = "LR-" + o.SellerOrderId });
        }
        await retailer.PostAsync($"/api/v1/orders/{done.OrderId}/confirm-delivery", null);

        long[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
        async Task<JsonElement> Tab(string tab) => await PaymentHttpTests.JsonAsync(await seller.GetAsync("/api/v1/seller/orders?status=" + tab));
        Assert.Equal(new[] { fresh.SellerOrderId }, Ids(await Tab("new")));
        Assert.Equal(new[] { toPack.SellerOrderId }, Ids(await Tab("to_pack")));
        Assert.Equal(new[] { shipped.SellerOrderId }, Ids(await Tab("shipped")));
        Assert.Equal(new[] { done.SellerOrderId }, Ids(await Tab("done")));
        var all = await PaymentHttpTests.JsonAsync(await seller.GetAsync("/api/v1/seller/orders"));
        Assert.Equal(4, all.GetProperty("total").GetInt32()); // the unpaid order is invisible
        Assert.Equal(new[] { done.SellerOrderId, shipped.SellerOrderId }, Ids(all).Take(2));
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync($"/api/v1/seller/orders/{unpaid.SellerOrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.PostAsync($"/api/v1/seller/orders/{unpaid.SellerOrderId}/accept", null)).StatusCode);

        var row = (await Tab("new")).GetProperty("items")[0];
        Assert.Equal("PendingAcceptance", row.GetProperty("status").GetString());
        Assert.Equal("Retailer", row.GetProperty("retailerName").GetString());
        Assert.Equal(28800m + 1440m + 1200m, row.GetProperty("orderValue").GetDecimal());
        Assert.Equal(576m, row.GetProperty("platformFee").GetDecimal());
        Assert.Equal(31440m - 576m, row.GetProperty("youGet").GetDecimal());
        Assert.Equal("Indore", row.GetProperty("deliveryCity").GetString());
        Assert.NotEqual(JsonValueKind.Null, row.GetProperty("acceptDeadline").ValueKind);
        foreach (var bad in new[] { "?status=nope", "?page=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.GetAsync("/api/v1/seller/orders" + bad)).StatusCode);
    }

    [Fact]
    public async Task Seller_actions_follow_the_state_machine_and_validate_inputAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (_, id) = await PlaceAsync(retailer);
        var url = $"/api/v1/seller/orders/{id}";

        Assert.Equal(HttpStatusCode.Conflict, (await seller.PostAsJsonAsync(url + "/dispatch", new { lrNumber = "LR-1" })).StatusCode); // not accepted yet
        var accepted = await PaymentHttpTests.JsonAsync(await seller.PostAsync(url + "/accept", null));
        Assert.Equal("Accepted", Status(accepted));
        Assert.Equal(JsonValueKind.Null, accepted.GetProperty("acceptDeadline").ValueKind);
        var again = await seller.PostAsync(url + "/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("invalid_order_transition", (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await seller.PostAsJsonAsync(url + "/reject", new { reason = "Out of stock" })).StatusCode); // too late to reject
        foreach (var lr in new[] { "", "   ", new string('x', 61) })
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync(url + "/dispatch", new { lrNumber = lr })).StatusCode);
        var dispatched = await PaymentHttpTests.JsonAsync(await seller.PostAsJsonAsync(url + "/dispatch", new { lrNumber = " LR-77 ", carrierName = "VRL" }));
        Assert.Equal("Dispatched", Status(dispatched));
        Assert.Equal("LR-77", dispatched.GetProperty("shipment").GetProperty("lrNumber").GetString());
        Assert.Equal("VRL", dispatched.GetProperty("shipment").GetProperty("carrierName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync(url + "/status", new { status = "Delivered" })).StatusCode); // only the retailer confirms delivery
        var timeline = dispatched.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("status").GetString()).ToArray();
        Assert.Equal(new[] { "Pending", "Pending", "Accepted", "Dispatched" }, timeline);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "retailer-user" && x.Title == "Order shipped"));
    }

    [Fact]
    public async Task Rejecting_releases_stock_refunds_in_full_and_needs_a_reasonAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (orderId, id) = await PlaceAsync(retailer);
        var url = $"/api/v1/seller/orders/{id}";
        foreach (var reason in new[] { "", "  " })
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync(url + "/reject", new { reason })).StatusCode);
        var rejected = await PaymentHttpTests.JsonAsync(await seller.PostAsJsonAsync(url + "/reject", new { reason = "Out of stock" }));
        Assert.Equal("Rejected", Status(rejected));
        Assert.Equal("Out of stock", rejected.GetProperty("rejectReason").GetString());

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1000, (await db.ProductVariants.SingleAsync(x => x.Id == 1)).StockQty);
        var refund = await db.Refunds.SingleAsync(x => x.SellerOrderId == id);
        Assert.Equal(31440m, refund.Amount);
        Assert.Equal("Processed", refund.Status);
        Assert.StartsWith("dev_refund_", refund.GatewayRefundId);
        Assert.Equal("Refunded", (await db.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus);
        var detail = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
        Assert.Equal("problems", detail.GetProperty("group").GetString());
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "retailer-user" && x.Title == "Order rejected"));
    }

    [Fact]
    public async Task Unaccepted_orders_expire_cancel_and_refund_with_a_fake_clockAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var waiting = await PlaceAsync(retailer);
        var accepted = await PlaceAsync(retailer);
        await seller.PostAsync($"/api/v1/seller/orders/{accepted.SellerOrderId}/accept", null);
        var unpaid = await PlaceAsync(retailer, pay: false);

        async Task<int> RunAsync()
        {
            using var scope = fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
        }
        fixture.Clock.Advance(TimeSpan.FromHours(23));
        Assert.Equal(1, await RunAsync()); // only the unpaid order passed its 60 minute payment window
        fixture.Clock.Advance(TimeSpan.FromHours(2)); // 25 hours after payment: window is 24 hours
        var late = await seller.PostAsync($"/api/v1/seller/orders/{waiting.SellerOrderId}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("accept_window_expired", (await late.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(1, await RunAsync());
        Assert.Equal(0, await RunAsync()); // nothing left to do; repeating is harmless

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var cancelled = await db.SellerOrders.Include(x => x.History).SingleAsync(x => x.Id == waiting.SellerOrderId);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("The seller did not accept in time.", cancelled.RejectReason);
        Assert.Equal("Cancelled", cancelled.History.OrderBy(x => x.Id).Last().Status);
        var refund = await db.Refunds.SingleAsync(x => x.SellerOrderId == waiting.SellerOrderId);
        Assert.Equal(31440m, refund.Amount);
        Assert.Equal("Processed", refund.Status);
        Assert.Equal("Refunded", (await db.Orders.SingleAsync(x => x.Id == waiting.OrderId)).PaymentStatus);
        Assert.Equal("Accepted", (await db.SellerOrders.SingleAsync(x => x.Id == accepted.SellerOrderId)).Status);
        Assert.Equal("Cancelled", (await db.Orders.SingleAsync(x => x.Id == unpaid.OrderId)).PaymentStatus);
        Assert.Empty(await db.Refunds.Where(x => x.SellerOrderId == unpaid.SellerOrderId).ToListAsync()); // never paid, nothing to refund
        Assert.Equal(1000 - 60, (await db.ProductVariants.SingleAsync(x => x.Id == 1)).StockQty); // only the accepted order still holds stock
    }

    [Fact]
    public async Task Payment_that_arrives_after_cancellation_is_refunded_not_keptAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var unpaid = await PlaceAsync(retailer, pay: false);
        var gatewayOrderId = (await PaymentHttpTests.JsonAsync(await PaymentHttpTests.StartPaymentAsync(retailer, unpaid.OrderId, "late-pay-key-1"))).GetProperty("gatewayOrderId").GetString()!;
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        using (var scope = fixture.Services.CreateScope()) Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync());
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null)).StatusCode);
        using (var scope = fixture.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
        using var check = fixture.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal("Refunded", (await db.Orders.SingleAsync(x => x.Id == unpaid.OrderId)).PaymentStatus);
        var refund = await db.Refunds.SingleAsync(x => x.SellerOrderId == unpaid.SellerOrderId);
        Assert.Equal(31440m, refund.Amount);
        Assert.Equal("Processed", refund.Status);
    }

    [Fact]
    public async Task Seller_orders_are_isolated_and_seller_onlyAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (_, id) = await PlaceAsync(retailer);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Businesses.Add(new TextileConnect.Domain.Businesses.Business { Id = 12, BusinessType = TextileConnect.Domain.Businesses.BusinessType.Manufacturer, LegalName = "Other Mill", DisplayName = "Other Mill", Phone = "+919000000012", VerificationStatus = TextileConnect.Domain.Businesses.VerificationStatus.Approved });
            await db.SaveChangesAsync();
        }
        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 12);
        Assert.Equal(0, (await PaymentHttpTests.JsonAsync(await other.GetAsync("/api/v1/seller/orders"))).GetProperty("total").GetInt32());
        foreach (var action in new[] { "accept", "reject", "dispatch" })
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/seller/orders/{id}/{action}", new { reason = "x", lrNumber = "LR-9" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/seller/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync("/api/v1/seller/orders/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/seller/orders")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/seller/orders")).StatusCode);
    }
}
