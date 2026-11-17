using Microsoft.AspNetCore.Builder;
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

public sealed class CheckoutHttpTests
{
    private const long RetailerId = 30;

    internal static async Task<MarketplaceHttpFixture> StartAsync(bool withRates = true, Action<WebApplication>? extra = null)
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => { app.MapCartEndpoints(); app.MapOrderEndpoints(); extra?.Invoke(app); });
        await CatalogueHttpTests.SeedAsync(fixture);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var indore = await db.Cities.SingleAsync(x => x.Name == "Indore");
        var ludhiana = await db.Cities.SingleAsync(x => x.Name == "Ludhiana");
        db.BusinessAddresses.AddRange(
            new BusinessAddress { Id = 100, BusinessId = RetailerId, AddressType = AddressType.Delivery, Line1 = "12 Market Road", CityId = indore.Id, Pincode = "452001", IsDefault = true },
            new BusinessAddress { Id = 101, BusinessId = 31, AddressType = AddressType.Delivery, Line1 = "Other shop", CityId = indore.Id, Pincode = "452001" });
        db.Businesses.AddRange(new Business { Id = 11, BusinessType = BusinessType.Manufacturer, LegalName = "Gupta Garments", DisplayName = "Gupta Garments", Phone = "+919000000011", VerificationStatus = VerificationStatus.Approved, CommissionRateOverride = 0.05m },
            new Business { Id = 31, BusinessType = BusinessType.Retailer, LegalName = "Other", DisplayName = "Other", Phone = "+919000000031", VerificationStatus = VerificationStatus.Approved });
        db.BusinessAddresses.Add(new BusinessAddress { Id = 102, BusinessId = 11, AddressType = AddressType.Pickup, Line1 = "Gupta factory", CityId = ludhiana.Id, Pincode = "141001", IsDefault = true });
        var category = await db.Categories.FirstAsync();
        db.Products.Add(new Product { Id = 5, SellerBusinessId = 11, CategoryId = category.Id, Title = "Cotton kurti", Slug = "kurti", Moq = 10, Status = ProductStatus.Active, GstRate = 12 });
        db.ProductVariants.Add(new ProductVariant { Id = 20, ProductId = 5, Sku = "KURTI-M", Size = "M", Colour = "Blue", StockQty = 100 });
        db.PriceSlabs.Add(new PriceSlab { ProductId = 5, MinQty = 10, PricePerUnit = 100 });
        await db.SaveChangesAsync();
        if (withRates)
        {
            var from = new DeliveryZone { Name = "Punjab", PincodePrefix = "14" };
            var to = new DeliveryZone { Name = "Central", PincodePrefix = "45" };
            db.DeliveryZones.AddRange(from, to);
            await db.SaveChangesAsync();
            db.DeliveryZoneRates.Add(new DeliveryZoneRate { FromZoneId = from.Id, ToZoneId = to.Id, RateType = ShippingRateType.PerPiece, Rate = 20m, MinCharge = 1000m, TransitDays = 4 });
            await db.SaveChangesAsync();
        }
        return fixture;
    }

    private static async Task AddToCartAsync(HttpClient client, long variantId, int quantity)
        => Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity })).StatusCode);

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, string? key, long addressId = 100)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(new { deliveryAddressId = addressId }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    [Fact]
    public async Task Checkout_splits_by_seller_and_computes_every_amount_on_the_serverAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: RetailerId);
        await AddToCartAsync(client, 1, 60);
        await AddToCartAsync(client, 20, 10);
        var response = await CheckoutAsync(client, "checkout-key-001");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Seller 10: 60 x 480 = 28,800 + GST 5% 1,440 + freight max(20 x 60, 1000) = 1,200.
        // Seller 11: 10 x 100 = 1,000 + GST 12% 120 + freight min charge 1,000.
        Assert.Equal(29800m, order.GetProperty("subTotal").GetDecimal());
        Assert.Equal(1560m, order.GetProperty("taxTotal").GetDecimal());
        Assert.Equal(2200m, order.GetProperty("shippingTotal").GetDecimal());
        Assert.Equal(33560m, order.GetProperty("grandTotal").GetDecimal());
        Assert.Equal("Pending", order.GetProperty("paymentStatus").GetString());
        Assert.Equal("452001", order.GetProperty("deliveryAddress").GetProperty("pincode").GetString());
        var sellers = order.GetProperty("sellerOrders").EnumerateArray().OrderBy(x => x.GetProperty("subTotal").GetDecimal()).Reverse().ToList();
        Assert.Equal(2, sellers.Count);
        Assert.Equal(28800m, sellers[0].GetProperty("subTotal").GetDecimal());
        Assert.Equal(576m, sellers[0].GetProperty("commissionAmount").GetDecimal()); // default 2%
        Assert.Equal(31440m, sellers[0].GetProperty("total").GetDecimal());
        Assert.Equal(50m, sellers[1].GetProperty("commissionAmount").GetDecimal()); // seller override 5%
        var item = sellers[0].GetProperty("items")[0];
        Assert.Equal("Quilted puffer jacket", item.GetProperty("title").GetString());
        Assert.Equal("L", item.GetProperty("size").GetString());
        Assert.Equal(480m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(5m, item.GetProperty("gstRate").GetDecimal());
        Assert.Equal("PendingAcceptance", sellers[0].GetProperty("status").GetString());
        Assert.Single(sellers[0].GetProperty("history").EnumerateArray());

        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/cart")).GetProperty("groups").EnumerateArray());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(940, (await db.ProductVariants.SingleAsync(x => x.Id == 1)).StockQty);
        Assert.Equal(90, (await db.ProductVariants.SingleAsync(x => x.Id == 20)).StockQty);
    }

    [Fact]
    public async Task Checkout_is_idempotent_for_the_same_key_and_rejects_key_reuseAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: RetailerId);
        await AddToCartAsync(client, 1, 60);
        var first = await CheckoutAsync(client, "checkout-key-002");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var number = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString();
        var replay = await CheckoutAsync(client, "checkout-key-002");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(number, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString());
        var reused = await CheckoutAsync(client, "checkout-key-002", addressId: 101);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("idempotency_key_reused", await CodeAsync(reused));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Orders.CountAsync(x => x.OrderNumber != "PAID-POPULARITY"));
        Assert.Equal(940, (await db.ProductVariants.SingleAsync(x => x.Id == 1)).StockQty);
        // The same key from another business is a different request.
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        await AddToCartAsync(other, 1, 60);
        Assert.Equal(HttpStatusCode.Created, (await CheckoutAsync(other, "checkout-key-002", addressId: 101)).StatusCode);
    }

    [Fact]
    public async Task Checkout_rejects_bad_input_and_unready_cartsAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", businessId: RetailerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await CheckoutAsync(client, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CheckoutAsync(client, "short")).StatusCode);
        var empty = await CheckoutAsync(client, "checkout-key-003");
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Equal("cart_empty", await CodeAsync(empty));
        await AddToCartAsync(client, 1, 10); // below MOQ 50
        var notReady = await CheckoutAsync(client, "checkout-key-004");
        Assert.Equal("cart_not_ready", await CodeAsync(notReady));
        await AddToCartAsync(client, 1, 40);
        Assert.Equal(HttpStatusCode.NotFound, (await CheckoutAsync(client, "checkout-key-005", addressId: 101)).StatusCode); // another business's address
        Assert.Equal(HttpStatusCode.NotFound, (await CheckoutAsync(client, "checkout-key-006", addressId: 999)).StatusCode);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller", 10);
        Assert.Equal(HttpStatusCode.Forbidden, (await CheckoutAsync(seller, "checkout-key-007")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CheckoutAsync(anonymous, "checkout-key-008")).StatusCode);
        // Nothing was created or reserved by the failed attempts.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(0, await db.Orders.CountAsync(x => x.OrderNumber != "PAID-POPULARITY"));
        Assert.Equal(1000, (await db.ProductVariants.SingleAsync(x => x.Id == 1)).StockQty);
    }

    [Fact]
    public async Task Checkout_fails_clearly_when_delivery_cannot_be_pricedAsync()
    {
        await using var fixture = await StartAsync(withRates: false);
        using var client = await fixture.ClientAsync("Retailer", businessId: RetailerId);
        await AddToCartAsync(client, 1, 60);
        var response = await CheckoutAsync(client, "checkout-key-009");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("shipping_unavailable", await CodeAsync(response));
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Orders.CountAsync(x => x.OrderNumber != "PAID-POPULARITY"));
    }
}
