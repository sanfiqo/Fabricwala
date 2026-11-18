using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class AdminListHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Admin, HttpClient Retailer, HttpClient Seller)> StartAsync()
    {
        var fixture = await PaymentHttpTests.StartAsync(app => { app.MapAdminListEndpoints(); app.MapReturnClaimEndpoints(); app.MapProductModerationEndpoints(); });
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.Orders.RemoveRange(db.Orders.Where(x => x.OrderNumber == "PAID-POPULARITY"));
            db.OrderItems.RemoveRange(db.OrderItems.Where(x => x.SellerOrder!.Order!.OrderNumber == "PAID-POPULARITY"));   // a real database refuses to delete a parent that still has lines
            db.SellerOrders.RemoveRange(db.SellerOrders.Where(x => x.Order!.OrderNumber == "PAID-POPULARITY"));
            await db.SaveChangesAsync();
        }
        return (fixture, await fixture.ClientAsync("Admin", "admin-user"), await fixture.ClientAsync("Retailer", "retailer-user", 30), await fixture.ClientAsync("Manufacturer", "seller-user", 10));
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
        => await PaymentHttpTests.JsonAsync(await client.GetAsync(url));

    private static long[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

    private static async Task ShipAsync(HttpClient seller, long sellerOrderId)
    {
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-42" });
    }

    [Fact]
    public async Task Orders_list_and_detail_cover_every_retailer_and_are_admin_onlyAsync()
    {
        var (fixture, admin, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = admin; using var ___ = retailer; using var ____ = seller;
        var paid = await SellerOrderHttpTests.PlaceAsync(retailer);
        var unpaid = await SellerOrderHttpTests.PlaceAsync(retailer, pay: false);

        var all = await GetAsync(admin, "/api/v1/admin/orders");
        Assert.Equal(new[] { unpaid.OrderId, paid.OrderId }, Ids(all)); // newest first
        Assert.Equal("Retailer", all.GetProperty("items")[0].GetProperty("retailerName").GetString());
        Assert.Equal("AwaitingPayment", all.GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal("WaitingForSeller", all.GetProperty("items")[1].GetProperty("status").GetString());
        Assert.Equal(new[] { paid.OrderId, unpaid.OrderId }.OrderBy(x => x), Ids(await GetAsync(admin, "/api/v1/admin/orders?status=active")).OrderBy(x => x));
        Assert.Empty(Ids(await GetAsync(admin, "/api/v1/admin/orders?status=delivered")));
        var number = all.GetProperty("items")[1].GetProperty("orderNumber").GetString()!;
        Assert.Equal(new[] { paid.OrderId }, Ids(await GetAsync(admin, "/api/v1/admin/orders?q=" + number[3..])));
        Assert.Single(Ids(await GetAsync(admin, "/api/v1/admin/orders?pageSize=1&page=2")));

        var detail = await GetAsync(admin, $"/api/v1/admin/orders/{paid.OrderId}");
        Assert.Equal(number, detail.GetProperty("orderNumber").GetString());
        Assert.Equal("Kapoor Knits", detail.GetProperty("sellerOrders")[0].GetProperty("manufacturerName").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/orders/99999")).StatusCode);
        foreach (var bad in new[] { "?status=nope", "?page=0", "?pageSize=101", "?q=" + new string('x', 41) })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/orders" + bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync($"/api/v1/admin/orders/{paid.OrderId}")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/orders")).StatusCode);
    }

    [Fact]
    public async Task Claims_list_detail_photo_and_resolve_workflowAsync()
    {
        var (fixture, admin, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = admin; using var ___ = retailer; using var ____ = seller;
        var first = await SellerOrderHttpTests.PlaceAsync(retailer);
        var second = await SellerOrderHttpTests.PlaceAsync(retailer);
        foreach (var o in new[] { first, second }) await ShipAsync(seller, o.SellerOrderId);
        var firstClaim = (await (await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{first.SellerOrderId}/claims", new { reason = "Damaged", pieces = 3, note = "Torn" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var secondClaim = (await (await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{second.SellerOrderId}/claims", new { reason = "WrongItem", pieces = 1 })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var png = ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Png, 40, 30, SkiaSharp.SKColors.Red);
        async Task<HttpResponseMessage> Upload(byte[] bytes)
        {
            var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return await retailer.PostAsync($"/api/v1/seller-orders/{first.SellerOrderId}/claims/{firstClaim}/photos", new MultipartFormDataContent { { file, "file", "p.png" } });
        }
        Assert.Equal(HttpStatusCode.OK, (await Upload(png)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(new byte[16])).StatusCode); // content type claims PNG but the bytes are not an image: refused at upload

        var open = await GetAsync(admin, "/api/v1/admin/claims?status=open");
        Assert.Equal(2, open.GetProperty("total").GetInt32());
        var row = open.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == firstClaim);
        Assert.Equal("Damaged", row.GetProperty("reason").GetString());
        Assert.Equal("Retailer", row.GetProperty("retailerName").GetString());
        Assert.Equal("Kapoor Knits", row.GetProperty("sellerName").GetString());
        Assert.Equal(1, row.GetProperty("photoCount").GetInt32());

        var detail = await GetAsync(admin, $"/api/v1/admin/claims/{firstClaim}");
        Assert.Equal(31440m, detail.GetProperty("orderTotal").GetDecimal());
        Assert.Equal(3, detail.GetProperty("claim").GetProperty("pieces").GetInt32());
        var photos = detail.GetProperty("photos").EnumerateArray().ToList();
        var image = await admin.GetAsync(photos[0].GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Contains("nosniff", image.Headers.GetValues("X-Content-Type-Options"));
        Assert.Single(photos);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/claims/{secondClaim}/photos/{photos[0].GetProperty("id").GetInt64()}")).StatusCode); // photo of another claim
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync(photos[0].GetProperty("url").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/claims/99999")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/claims/{firstClaim}/decide", new { decision = "Rejected", refundAmount = 0, note = "Normal wear" })).StatusCode);
        Assert.Equal(new[] { firstClaim }, Ids(await GetAsync(admin, "/api/v1/admin/claims?status=resolved")));
        var everything = await GetAsync(admin, "/api/v1/admin/claims");
        Assert.Equal(new[] { secondClaim, firstClaim }, Ids(everything)); // open claims come first
        foreach (var bad in new[] { "?status=nope", "?page=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/claims" + bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/admin/claims")).StatusCode);
    }

    [Fact]
    public async Task Seller_and_retailer_lists_show_status_city_and_figuresAsync()
    {
        var (fixture, admin, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = admin; using var ___ = retailer; using var ____ = seller;
        await SellerOrderHttpTests.PlaceAsync(retailer);
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var city = db.Cities.First(x => x.Name == "Ludhiana");
            db.BusinessAddresses.Add(new TextileConnect.Domain.Businesses.BusinessAddress { BusinessId = 10, AddressType = TextileConnect.Domain.Businesses.AddressType.Registered, Line1 = "Plot 7", CityId = city.Id, Pincode = "141001" });
            await db.SaveChangesAsync();
        }

        var sellers = await GetAsync(admin, "/api/v1/admin/sellers");
        Assert.Equal(3, sellers.GetProperty("total").GetInt32()); // Kapoor Knits, Gupta Garments and the pending seller; retailers are not listed
        var kapoor = sellers.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Kapoor Knits");
        Assert.Equal("Ludhiana", kapoor.GetProperty("city").GetString());
        Assert.Equal("Approved", kapoor.GetProperty("status").GetString());
        Assert.Equal(1, kapoor.GetProperty("orders").GetInt32()); // the rejected order is not counted
        Assert.Equal(28800m, kapoor.GetProperty("value").GetDecimal());
        Assert.Equal(576m, kapoor.GetProperty("commission").GetDecimal());
        Assert.Equal(2, kapoor.GetProperty("activeProducts").GetInt32());
        Assert.Equal("Pending", (await GetAsync(admin, "/api/v1/admin/sellers?status=pending")).GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(1, (await GetAsync(admin, "/api/v1/admin/sellers?q=Kapoor")).GetProperty("total").GetInt32());

        var retailers = await GetAsync(admin, "/api/v1/admin/retailers");
        Assert.Equal(2, retailers.GetProperty("total").GetInt32()); // the shop that ordered and the idle retailer from the checkout seed
        var shop = retailers.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Retailer");
        Assert.Equal("Retailer", shop.GetProperty("name").GetString());
        Assert.Equal(1, shop.GetProperty("orders").GetInt32()); // the refunded order is not spend
        Assert.Equal(31440m, shop.GetProperty("value").GetDecimal());
        foreach (var url in new[] { "/api/v1/admin/sellers?status=nope", "/api/v1/admin/retailers?page=0", "/api/v1/admin/sellers?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/admin/sellers")).StatusCode);
    }

    [Fact]
    public async Task Product_moderation_list_is_paged_filterable_and_shows_admin_holdsAsync()
    {
        var (fixture, admin, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = admin; using var ___ = retailer; using var ____ = seller;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/products/2/moderate", new { action = "Deactivate", reason = "Policy" })).StatusCode);
        var all = await GetAsync(admin, "/api/v1/admin/products");
        Assert.Equal(5, all.GetProperty("total").GetInt32()); // four from the catalogue seed plus the kurti from the checkout seed
        var puffer = all.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "puffer");
        Assert.Equal("Kapoor Knits", puffer.GetProperty("sellerName").GetString());
        Assert.Equal(440m, puffer.GetProperty("priceFrom").GetDecimal());
        Assert.Equal(new[] { 2L }, Ids(await GetAsync(admin, "/api/v1/admin/products?held=true")));
        Assert.Equal(new[] { 2L }, Ids(await GetAsync(admin, "/api/v1/admin/products?status=inactive")));
        Assert.Equal(new[] { 3L }, Ids(await GetAsync(admin, "/api/v1/admin/products?status=draft")));
        Assert.Equal(new[] { 1L }, Ids(await GetAsync(admin, "/api/v1/admin/products?q=puff")));
        Assert.Equal(2, (await GetAsync(admin, "/api/v1/admin/products?pageSize=2")).GetProperty("items").GetArrayLength());
        foreach (var bad in new[] { "?status=nope", "?page=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/products" + bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Payout_list_and_commission_report_add_up_with_csv_and_validationAsync()
    {
        var (fixture, admin, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = admin; using var ___ = retailer; using var ____ = seller;
        await SellerOrderHttpTests.PlaceAsync(retailer); // stays on hold
        var shipped = await SellerOrderHttpTests.PlaceAsync(retailer);
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);
        await ShipAsync(seller, shipped.SellerOrderId);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });

        var payouts = await GetAsync(admin, "/api/v1/admin/payouts");
        Assert.Equal(3, payouts.GetProperty("payouts").GetProperty("total").GetInt32());
        Assert.Equal(30864m, payouts.GetProperty("onHoldAmount").GetDecimal());
        Assert.Equal(30864m, payouts.GetProperty("scheduledAmount").GetDecimal());
        Assert.All(payouts.GetProperty("payouts").GetProperty("items").EnumerateArray(), x => Assert.Equal("Kapoor Knits", x.GetProperty("sellerName").GetString()));
        Assert.Equal(1, (await GetAsync(admin, "/api/v1/admin/payouts?status=cancelled")).GetProperty("payouts").GetProperty("total").GetInt32());
        Assert.Equal(1, (await GetAsync(admin, "/api/v1/admin/payouts?status=on_hold")).GetProperty("payouts").GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/payouts?status=nope")).StatusCode);

        var report = await GetAsync(admin, "/api/v1/admin/commission");
        Assert.Equal(2, report.GetProperty("orders").GetInt32()); // the rejected order pays no commission
        Assert.Equal(57600m, report.GetProperty("goodsValue").GetDecimal());
        Assert.Equal(1152m, report.GetProperty("commission").GetDecimal());
        var row = report.GetProperty("sellers")[0];
        Assert.Equal("Kapoor Knits", row.GetProperty("sellerName").GetString());
        Assert.Equal(1152m, row.GetProperty("commission").GetDecimal());
        var today = fixture.Clock.GetUtcNow().UtcDateTime.Date;
        Assert.Equal(2, (await GetAsync(admin, $"/api/v1/admin/commission?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}")).GetProperty("orders").GetInt32());
        Assert.Equal(0, (await GetAsync(admin, $"/api/v1/admin/commission?from={today.AddDays(-30):yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}")).GetProperty("orders").GetInt32());
        var csv = await admin.GetAsync("/api/v1/admin/commission?csv=true");
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var lines = (await csv.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("sellerId,seller,orders,goodsValue,commission", lines[0]);
        Assert.Equal("10,Kapoor Knits,2,57600.00,1152.00", lines[1]);
        foreach (var bad in new[] { "?from=2026-13-01", "?from=today", "?from=2026-05-02&to=2026-05-01", "?from=2024-01-01&to=2026-01-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/commission" + bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/admin/commission")).StatusCode);
    }
}
