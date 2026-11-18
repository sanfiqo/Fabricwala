using TextileConnect.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class RetailerOrderHttpTests
{
    private const long RetailerId = 30;
    private static int keyCounter;

    private static async Task<(long OrderId, long SellerOrderId)> PlaceAsync(HttpClient retailer, bool pay)
    {
        var order = await PaymentHttpTests.PlaceOrderAsync(retailer);
        var orderId = order.GetProperty("id").GetInt64();
        if (pay)
        {
            var key = "list-pay-key-" + Interlocked.Increment(ref keyCounter);
            var gatewayOrderId = (await PaymentHttpTests.JsonAsync(await PaymentHttpTests.StartPaymentAsync(retailer, orderId, key))).GetProperty("gatewayOrderId").GetString();
            await PaymentHttpTests.JsonAsync(await retailer.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null));
        }
        return (orderId, order.GetProperty("sellerOrders")[0].GetProperty("id").GetInt64());
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, string query)
        => await PaymentHttpTests.JsonAsync(await client.GetAsync("/api/v1/orders" + query));

    [Fact]
    public async Task Orders_list_by_group_with_stable_paging_and_derived_statusAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using (var seedScope = fixture.Services.CreateScope())
        {
            // Drop the order the shared catalogue seed adds for popularity ranking so counts here are exact.
            var seedDb = seedScope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            seedDb.Orders.RemoveRange(seedDb.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            seedDb.OrderItems.RemoveRange(seedDb.OrderItems.Where(x => x.SellerOrder!.Order!.OrderNumber == "PAID-POPULARITY"));   // a real database refuses to delete a parent that still has lines
            seedDb.SellerOrders.RemoveRange(seedDb.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            await seedDb.SaveChangesAsync();
        }
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var unpaid = await PlaceAsync(retailer, pay: false);
        var packing = await PlaceAsync(retailer, pay: true);
        var delivered = await PlaceAsync(retailer, pay: true);
        var problem = await PlaceAsync(retailer, pay: true);
        var cancelled = await PlaceAsync(retailer, pay: true);

        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{packing.SellerOrderId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{delivered.SellerOrderId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{delivered.SellerOrderId}/dispatch", new { lrNumber = "LR-1001", carrierName = "VRL" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/orders/{delivered.OrderId}/confirm-delivery", null)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.ReturnClaims.Add(new ReturnClaim { SellerOrderId = problem.SellerOrderId, Reason = "Damaged" });
            (await db.SellerOrders.SingleAsync(x => x.Id == cancelled.SellerOrderId)).Status = SellerOrderStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        string[] Numbers(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64().ToString()).ToArray();
        string Id(long id) => id.ToString();
        var all = await ListAsync(retailer, "");
        Assert.Equal(5, all.GetProperty("total").GetInt32());
        Assert.Equal(new[] { Id(cancelled.OrderId), Id(problem.OrderId), Id(delivered.OrderId), Id(packing.OrderId), Id(unpaid.OrderId) }, Numbers(all)); // newest first
        Assert.Equal(new[] { Id(packing.OrderId), Id(unpaid.OrderId) }, Numbers(await ListAsync(retailer, "?group=active")));
        Assert.Equal(new[] { Id(delivered.OrderId) }, Numbers(await ListAsync(retailer, "?group=delivered")));
        Assert.Equal(new[] { Id(cancelled.OrderId), Id(problem.OrderId) }, Numbers(await ListAsync(retailer, "?group=problems")));

        var first = await ListAsync(retailer, "?pageSize=2&page=1");
        var second = await ListAsync(retailer, "?pageSize=2&page=2");
        var third = await ListAsync(retailer, "?pageSize=2&page=3");
        Assert.Equal(5, first.GetProperty("total").GetInt32());
        Assert.Equal(new[] { Id(cancelled.OrderId), Id(problem.OrderId) }, Numbers(first));
        Assert.Equal(new[] { Id(delivered.OrderId), Id(packing.OrderId) }, Numbers(second));
        Assert.Equal(new[] { Id(unpaid.OrderId) }, Numbers(third));

        string Status(long id) => all.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == id).GetProperty("status").GetString()!;
        Assert.Equal("AwaitingPayment", Status(unpaid.OrderId));
        Assert.Equal("Packing", Status(packing.OrderId));
        Assert.Equal("Delivered", Status(delivered.OrderId));
        Assert.Equal("ProblemReported", Status(problem.OrderId));
        Assert.Equal("Cancelled", Status(cancelled.OrderId));
        var row = all.GetProperty("items")[3];
        Assert.Equal("Quilted puffer jacket", row.GetProperty("firstItemTitle").GetString());
        Assert.Equal(60, row.GetProperty("totalQuantity").GetInt32());
        Assert.Equal("Kapoor Knits", row.GetProperty("sellerNames")[0].GetString());
        // The list carries the order lines, so a client needs no second request per order.
        var lines = row.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(row.GetProperty("itemCount").GetInt32(), lines.Count);
        Assert.Equal(60, lines.Sum(x => x.GetProperty("quantity").GetInt32()));
        Assert.Equal("Quilted puffer jacket", lines[0].GetProperty("title").GetString());
        Assert.True(lines[0].GetProperty("unitPrice").GetDecimal() > 0);
        Assert.True(lines[0].GetProperty("lineTotal").GetDecimal() > 0);

        foreach (var bad in new[] { "?group=nope", "?page=0", "?pageSize=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await retailer.GetAsync("/api/v1/orders" + bad)).StatusCode);
    }

    [Fact]
    public async Task Order_detail_has_timeline_shipment_and_confirm_delivery_rulesAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var (orderId, sellerOrderId) = await PlaceAsync(retailer, pay: true);

        var notShipped = await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        Assert.Equal(HttpStatusCode.Conflict, notShipped.StatusCode);
        Assert.Equal("order_not_shipped", (await notShipped.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-2002", carrierName = "VRL Logistics" });
        var shippedDetail = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Shipped", shippedDetail.GetProperty("status").GetString());
        Assert.Equal("active", shippedDetail.GetProperty("group").GetString());
        Assert.Equal("LR-2002", shippedDetail.GetProperty("sellerOrders")[0].GetProperty("shipment").GetProperty("lrNumber").GetString());

        var confirmed = await PaymentHttpTests.JsonAsync(await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null));
        Assert.Equal("Delivered", confirmed.GetProperty("status").GetString());
        Assert.Equal("delivered", confirmed.GetProperty("group").GetString());
        var seller0 = confirmed.GetProperty("sellerOrders")[0];
        Assert.Equal("Kapoor Knits", seller0.GetProperty("manufacturerName").GetString());
        Assert.Equal("Delivered", seller0.GetProperty("status").GetString());
        var timeline = seller0.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("status").GetString()).ToArray();
        Assert.Equal(new[] { "Pending", "Pending", "Accepted", "Dispatched", "Delivered" }, timeline);
        Assert.Equal("Indore", confirmed.GetProperty("deliveryAddress").GetProperty("city").GetString());
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null)).StatusCode); // repeat is harmless

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "seller-user" && x.Title == "Delivery confirmed"));
        Assert.Equal(1, await db.OrderStatusHistories.CountAsync(x => x.SellerOrderId == sellerOrderId && x.Status == "Delivered"));
    }

    [Fact]
    public async Task Retailer_orders_are_isolated_and_retailer_onlyAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        var (orderId, _) = await PlaceAsync(owner, pay: true);
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(0, (await ListAsync(other, "")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/v1/orders/99999")).StatusCode);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/orders")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/orders")).StatusCode);
    }
}
