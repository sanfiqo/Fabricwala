using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>
/// The whole marketplace over real HTTP endpoints (test server, in-memory data, test authentication, fake payment gateway):
/// retailer and seller sign up, admin approves, seller publishes, retailer searches, orders and pays, seller ships,
/// retailer confirms delivery and the payout is released. Sign-in itself (OTP, cookies) is covered by AuthEndpointTests.
/// </summary>
public sealed class FullJourneyHttpTests
{
    internal static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app =>
        {
            app.MapProductEndpoints(); app.MapLandedCostEndpoints(); app.MapCartEndpoints(); app.MapOrderEndpoints();
            app.MapPaymentEndpoints(); app.MapReturnClaimEndpoints(); app.MapPayoutEndpoints(); app.MapNotificationEndpoints(); app.MapAccountEndpoints();
        });
        // Platform data an admin would configure: freight between the Punjab and Madhya Pradesh pincode zones.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var from = new DeliveryZone { Name = "Punjab", PincodePrefix = "14" };
        var to = new DeliveryZone { Name = "Central", PincodePrefix = "45" };
        db.DeliveryZones.AddRange(from, to);
        await db.SaveChangesAsync();
        db.DeliveryZoneRates.Add(new DeliveryZoneRate { FromZoneId = from.Id, ToZoneId = to.Id, RateType = ShippingRateType.PerPiece, Rate = 20m, MinCharge = 1000m, TransitDays = 4 });
        await db.SaveChangesAsync();
        return fixture;
    }

    internal static async Task<long> SignUpAsync(HttpClient client, string type, string name, string pincode)
    {
        var response = await client.PostAsJsonAsync("/api/v1/businesses", new { businessType = type, legalName = name, displayName = name, pincode });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return profile.GetProperty("id").GetInt64();
    }

    /// <summary>Steps 1-3 of the journey: sign up, admin approval, seller address and published product, retailer delivery address.</summary>
    internal static async Task<(HttpClient Retailer, HttpClient Seller, HttpClient Admin, long RetailerId, long SellerId, long VariantId, string Slug, long AddressId)> PrepareAsync(MarketplaceHttpFixture fixture)
    {
        // 1. Both sides sign up. Until approval they act with an unapproved business.
        var newRetailer = await fixture.ClientAsync("Retailer", "journey-retailer");
        var newSeller = await fixture.ClientAsync("Manufacturer", "journey-seller");
        var retailerId = await SignUpAsync(newRetailer, "Retailer", "Sharma Textiles", "452001");
        var sellerId = await SignUpAsync(newSeller, "Manufacturer", "Kapoor Knits", "141001");
        var unapprovedSeller = await fixture.ClientAsync("Manufacturer", "journey-seller", sellerId, approved: false);
        var unapprovedRetailer = await fixture.ClientAsync("Retailer", "journey-retailer", retailerId, approved: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await unapprovedSeller.PostAsJsonAsync("/api/v1/seller/products", new { title = "Early jacket", publish = false })).StatusCode); // rule 1
        Assert.Equal(HttpStatusCode.Forbidden, (await unapprovedRetailer.GetAsync("/api/v1/cart")).StatusCode);

        // 2. Admin approves both.
        var admin = await fixture.ClientAsync("Admin", "journey-admin");
        var pending = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/businesses?status=pending");
        Assert.Equal(2, pending.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/admin/businesses/{sellerId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/admin/businesses/{retailerId}/approve", null)).StatusCode);
        var seller = await fixture.ClientAsync("Manufacturer", "journey-seller", sellerId);
        var retailer = await fixture.ClientAsync("Retailer", "journey-retailer", retailerId);

        // 3. Seller adds a pickup address and publishes a product with price tiers.
        Assert.Equal(HttpStatusCode.Created, (await seller.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Factory gate 2, Focal Point", pincode = "141001" })).StatusCode);
        long categoryId;
        using (var scope = fixture.Services.CreateScope())
            categoryId = (await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Categories.SingleAsync(x => x.Slug == "winter-wear")).Id;
        var published = await seller.PostAsJsonAsync("/api/v1/seller/products", new
        {
            title = "Quilted puffer jacket", categoryId, material = "Polyester", gstRate = 5, moq = 50, unit = 0, weightPerUnitGrams = 800, dispatchDays = 3,
            variants = new[] { new { size = "L", colour = "Black", stockQty = 500 } },
            tiers = new[] { new { minQty = 50, pricePerUnit = 480m }, new { minQty = 200, pricePerUnit = 459m }, new { minQty = 500, pricePerUnit = 440m } },
            publish = true
        });
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var product = await published.Content.ReadFromJsonAsync<JsonElement>();
        var slug = product.GetProperty("slug").GetString()!;
        var variantId = product.GetProperty("variants")[0].GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.Created, (await retailer.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "12 Market Road", pincode = "452001" })).StatusCode);
        var addressId = (await retailer.GetFromJsonAsync<JsonElement>("/api/v1/account/addresses"))[0].GetProperty("id").GetInt64();
        return (retailer, seller, admin, retailerId, sellerId, variantId, slug, addressId);
    }

    internal static async Task<(long OrderId, long SellerOrderId, decimal Total)> OrderAsync(HttpClient retailer, long variantId, long addressId, string key, bool pay)
    {
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 100 })).StatusCode);
        using var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(new { deliveryAddressId = addressId }) };
        checkoutRequest.Headers.Add("Idempotency-Key", key);
        var order = await (await retailer.SendAsync(checkoutRequest)).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetInt64();
        if (pay)
        {
            using var payRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments") { Content = JsonContent.Create(new { orderId }) };
            payRequest.Headers.Add("Idempotency-Key", key + "-pay");
            var session = await (await retailer.SendAsync(payRequest)).Content.ReadFromJsonAsync<JsonElement>();
            await retailer.PostAsync($"/api/v1/payments/{session.GetProperty("gatewayOrderId").GetString()}/dev-complete", null);
        }
        return (orderId, order.GetProperty("sellerOrders")[0].GetProperty("id").GetInt64(), order.GetProperty("grandTotal").GetDecimal());
    }

    [Fact]
    public async Task FullJourneyAsync()
    {
        await using var fixture = await StartAsync();

        var (retailer, seller, admin, retailerId, sellerId, variantId, slug, addressId) = await PrepareAsync(fixture);

        // 4. Retailer finds it, checks the landed price, adds to the cart and checks out.
        var search = await retailer.GetFromJsonAsync<JsonElement>("/api/v1/products?q=puffer&pincode=452001");
        Assert.Equal(1, search.GetProperty("total").GetInt32());
        Assert.Equal(slug, search.GetProperty("items")[0].GetProperty("slug").GetString());
        var detail = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/products/{slug}?pincode=452001&qty=100");
        Assert.Equal(480m, detail.GetProperty("landedCost").GetProperty("slabPrice").GetDecimal());
        Assert.Equal(480m * 100 + 2400m + 2000m, detail.GetProperty("landedCost").GetProperty("total").GetDecimal()); // goods + 5% GST + freight 20 x 100

        var cart = await retailer.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 100 });
        Assert.Equal(HttpStatusCode.OK, cart.StatusCode);
        var cartBody = await cart.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cartBody.GetProperty("canCheckout").GetBoolean());
        Assert.Equal(200 - 100, cartBody.GetProperty("groups")[0].GetProperty("items")[0].GetProperty("nextTier").GetProperty("addQuantity").GetInt32());
        using var checkoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(new { deliveryAddressId = addressId }) };
        checkoutRequest.Headers.Add("Idempotency-Key", "journey-checkout-1");
        var checkout = await retailer.SendAsync(checkoutRequest);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetInt64();
        var sellerOrderId = order.GetProperty("sellerOrders")[0].GetProperty("id").GetInt64();
        const decimal total = 48000m + 2400m + 2000m;
        Assert.Equal(total, order.GetProperty("grandTotal").GetDecimal());

        // 5. Retailer pays with the test gateway.
        using var payRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments") { Content = JsonContent.Create(new { orderId }) };
        payRequest.Headers.Add("Idempotency-Key", "journey-payment-1");
        var session = await (await retailer.SendAsync(payRequest)).Content.ReadFromJsonAsync<JsonElement>();
        var paid = await (await retailer.PostAsync($"/api/v1/payments/{session.GetProperty("gatewayOrderId").GetString()}/dev-complete", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Paid", paid.GetProperty("status").GetString());

        // 6. Seller sees the new order, accepts and ships it.
        var newOrders = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/orders?status=new");
        Assert.Equal(sellerOrderId, newOrders.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(total, newOrders.GetProperty("items")[0].GetProperty("orderValue").GetDecimal());
        Assert.Equal(960m, newOrders.GetProperty("items")[0].GetProperty("platformFee").GetDecimal()); // 2% of the 48,000 goods value
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null)).StatusCode);
        var shipped = await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-7788", carrierName = "VRL" });
        Assert.Equal("Dispatched", (await shipped.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var tracking = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Shipped", tracking.GetProperty("status").GetString());
        Assert.Equal("LR-7788", tracking.GetProperty("sellerOrders")[0].GetProperty("shipment").GetProperty("lrNumber").GetString());
        var scheduled = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/payouts");
        Assert.Equal("Scheduled", scheduled.GetProperty("payouts").GetProperty("items")[0].GetProperty("status").GetString());

        // 7. Retailer confirms delivery; the payout is released.
        var delivered = await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        Assert.Equal("Delivered", (await delivered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        using (var scope = fixture.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
        var payouts = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/payouts");
        Assert.Equal("Paid", payouts.GetProperty("payouts").GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(total - 960m, payouts.GetProperty("paidThisMonth").GetDecimal());
        Assert.Equal(0m, payouts.GetProperty("onHoldAmount").GetDecimal());

        // 8. Everyone was told along the way, and the retailer's list files the order under delivered.
        var sellerNotes = (await seller.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("title").GetString()).ToList();
        Assert.Contains("New order", sellerNotes);
        Assert.Contains("Delivery confirmed", sellerNotes);
        Assert.Contains("Payout released", sellerNotes);
        var retailerNotes = (await retailer.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("title").GetString()).ToList();
        Assert.Contains("Order shipped", retailerNotes);
        Assert.Single((await retailer.GetFromJsonAsync<JsonElement>("/api/v1/orders?group=delivered")).GetProperty("items").EnumerateArray());
        Assert.Single((await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/orders?status=done")).GetProperty("items").EnumerateArray());
        using (var scope = fixture.Services.CreateScope())
            Assert.Equal(500 - 100, (await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().ProductVariants.SingleAsync(x => x.Id == variantId)).StockQty);
    }
}
