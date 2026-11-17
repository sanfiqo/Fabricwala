using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class RetailerExtrasHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Retailer, HttpClient Seller)> StartAsync()
    {
        var fixture = await PaymentHttpTests.StartAsync(app => { app.MapRetailerExtrasEndpoints(); app.MapInvoiceEndpoints(); app.MapProductEndpoints(); });
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

    private static async Task<JsonElement> GetAsync(HttpClient client, string url) => await PaymentHttpTests.JsonAsync(await client.GetAsync(url));

    private static async Task ShipAsync(HttpClient seller, long sellerOrderId)
    {
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-8" });
    }

    [Fact]
    public async Task Saved_products_are_idempotent_listed_with_availability_and_privateAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        Assert.Equal(HttpStatusCode.OK, (await retailer.PutAsync("/api/v1/saved-products/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await retailer.PutAsync("/api/v1/saved-products/1", null)).StatusCode); // saving twice keeps one row
        Assert.Equal(HttpStatusCode.OK, (await retailer.PutAsync("/api/v1/saved-products/2", null)).StatusCode);
        foreach (var id in new[] { 3, 4, 99999 }) // draft, unapproved seller, missing
            Assert.Equal(HttpStatusCode.NotFound, (await retailer.PutAsync($"/api/v1/saved-products/{id}", null)).StatusCode);
        var list = await GetAsync(retailer, "/api/v1/saved-products");
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        var first = list.GetProperty("items")[0];
        Assert.Equal("fleece-hoodie", first.GetProperty("slug").GetString()); // most recently saved first
        Assert.Equal("Kapoor Knits", first.GetProperty("sellerName").GetString());
        Assert.Equal(300m, first.GetProperty("priceFrom").GetDecimal());
        Assert.True(first.GetProperty("available").GetBoolean());
        await seller.PutAsJsonAsync("/api/v1/seller/products/2/active", new { active = false });
        Assert.False((await GetAsync(retailer, "/api/v1/saved-products")).GetProperty("items")[0].GetProperty("available").GetBoolean()); // stays listed, marked unavailable
        Assert.Single((await GetAsync(retailer, "/api/v1/saved-products?pageSize=1&page=2")).GetProperty("items").EnumerateArray());

        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(0, (await GetAsync(other, "/api/v1/saved-products")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await other.DeleteAsync("/api/v1/saved-products/1")).StatusCode); // cannot remove someone else's
        Assert.Equal(2, (await GetAsync(retailer, "/api/v1/saved-products")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await retailer.DeleteAsync("/api/v1/saved-products/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await retailer.DeleteAsync("/api/v1/saved-products/1")).StatusCode); // repeat is fine
        Assert.Equal(1, (await GetAsync(retailer, "/api/v1/saved-products")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/saved-products")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await retailer.GetAsync("/api/v1/saved-products?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task A_delivered_order_can_be_rated_and_the_rating_updatedAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (orderId, sellerOrderId) = await SellerOrderHttpTests.PlaceAsync(retailer);
        var early = await retailer.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 5 });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("order_not_delivered", (await early.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await retailer.GetAsync($"/api/v1/orders/{orderId}/rating")).StatusCode);
        await ShipAsync(seller, sellerOrderId);
        await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);

        foreach (var bad in new object[] { new { stars = 0 }, new { stars = 6 }, new { stars = 4, comment = new string('x', 1001) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await retailer.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", bad)).StatusCode);
        var rated = await PaymentHttpTests.JsonAsync(await retailer.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 4, comment = "  Good fabric  " }));
        Assert.Equal(4, rated.GetProperty("stars").GetInt32());
        Assert.Equal("Good fabric", rated.GetProperty("comment").GetString());
        await PaymentHttpTests.JsonAsync(await retailer.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 2 }));
        var current = await GetAsync(retailer, $"/api/v1/orders/{orderId}/rating");
        Assert.Equal(2, current.GetProperty("stars").GetInt32());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("comment").ValueKind);
        using (var scope = fixture.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().OrderRatings.CountAsync()); // updated, not duplicated

        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/orders/{orderId}/rating")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 5 })).StatusCode);
    }

    [Fact]
    public async Task Pincode_check_tells_whether_delivery_is_possibleAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var indore = await GetAsync(retailer, "/api/v1/account/pincode-check?pincode=452001");
        Assert.Equal("Indore", indore.GetProperty("city").GetString());
        Assert.Equal("Madhya Pradesh", indore.GetProperty("state").GetString());
        Assert.True(indore.GetProperty("deliverable").GetBoolean()); // freight into this zone is configured
        Assert.False((await GetAsync(retailer, "/api/v1/account/pincode-check?pincode=141001")).GetProperty("deliverable").GetBoolean()); // known place, but no freight into its zone
        Assert.Equal(HttpStatusCode.BadRequest, (await retailer.GetAsync("/api/v1/account/pincode-check?pincode=12")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await retailer.GetAsync("/api/v1/account/pincode-check")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await retailer.GetAsync("/api/v1/account/pincode-check?pincode=999999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/v1/account/pincode-check?pincode=452001")).StatusCode);
    }

    [Fact]
    public async Task Reorder_fills_the_cart_and_reports_what_it_had_to_skip_or_reduceAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (orderId, _) = await SellerOrderHttpTests.PlaceAsync(retailer, pay: false); // 60 pieces of variant 1, stock now 940
        async Task<JsonElement> Reorder() => await PaymentHttpTests.JsonAsync(await retailer.PostAsync($"/api/v1/orders/{orderId}/reorder", null));
        async Task SetStock(int stock, bool active = true)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == 1);
            variant.StockQty = stock; variant.IsActive = active;
            await db.SaveChangesAsync();
        }

        var full = await Reorder();
        Assert.Equal(60, full.GetProperty("added")[0].GetProperty("quantity").GetInt32());
        Assert.Equal("Quilted puffer jacket", full.GetProperty("added")[0].GetProperty("title").GetString());
        Assert.Empty(full.GetProperty("skipped").EnumerateArray());
        Assert.Equal(60, full.GetProperty("cart").GetProperty("groups")[0].GetProperty("items")[0].GetProperty("quantity").GetInt32());

        await SetStock(80); // the cart already holds 60, so only 20 more exist
        var reduced = await Reorder();
        Assert.Equal(20, reduced.GetProperty("added")[0].GetProperty("quantity").GetInt32());
        Assert.Equal("reduced_to_stock", reduced.GetProperty("added")[0].GetProperty("reason").GetString());
        await SetStock(80);
        var none = await Reorder();
        Assert.Empty(none.GetProperty("added").EnumerateArray());
        Assert.Equal("out_of_stock", none.GetProperty("skipped")[0].GetProperty("reason").GetString());
        await SetStock(500, active: false);
        Assert.Equal("not_available", (await Reorder()).GetProperty("skipped")[0].GetProperty("reason").GetString());

        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/orders/{orderId}/reorder", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsync($"/api/v1/orders/{orderId}/reorder", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await retailer.PostAsync("/api/v1/orders/99999/reorder", null)).StatusCode);
    }

    [Fact]
    public async Task The_platform_fee_carries_gst_once_the_platform_company_is_set_up_and_comes_out_of_the_payoutAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var before = await SellerOrderHttpTests.PlaceAsync(retailer);                 // placed while the platform company is not set up
        var gstin = "08AAAAA0000A1Z"; gstin += TextileConnect.Application.Businesses.GstinRules.CheckCharacter(gstin + "0");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            foreach (var (key, value) in new[] { ("Platform.LegalName", "Fabricwala Pvt Ltd"), ("Platform.Gstin", gstin), ("Platform.Address", "9 Tech Park"), ("Platform.Pincode", "302001") })
                db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var after = await SellerOrderHttpTests.PlaceAsync(retailer);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var old = await db.SellerOrders.SingleAsync(x => x.Id == before.SellerOrderId);
            var fresh = await db.SellerOrders.SingleAsync(x => x.Id == after.SellerOrderId);
            Assert.Equal(0m, old.CommissionGst);                                         // an order made earlier keeps its terms
            Assert.True(fresh.CommissionAmount > 0);
            Assert.Equal(decimal.Round(fresh.CommissionAmount * 0.18m, 2, MidpointRounding.AwayFromZero), fresh.CommissionGst);
            var oldPayout = await db.Settlements.SingleAsync(x => x.SellerOrderId == before.SellerOrderId);
            var freshPayout = await db.Settlements.SingleAsync(x => x.SellerOrderId == after.SellerOrderId);
            Assert.Equal(old.Total - old.CommissionAmount, oldPayout.Amount);
            Assert.Equal(fresh.Total - fresh.CommissionAmount - fresh.CommissionGst, freshPayout.Amount);   // the fee and its GST both come out of the payout
        }

        await MakeInvoiceableAsync(fixture, after.SellerOrderId);
        var fee = await seller.GetAsync($"/api/v1/invoices/seller-orders/{after.SellerOrderId}/download?type=PlatformCommission");
        Assert.Equal(HttpStatusCode.OK, fee.StatusCode);
        Assert.Matches(@"^PF-\d{4}-00001\.pdf$", fee.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
    }

    [Fact]
    public async Task An_order_line_shows_the_product_photo_as_it_was_at_checkoutAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var first = await SellerOrderHttpTests.PlaceAsync(retailer, pay: false);
        var firstUrl = (await GetAsync(retailer, $"/api/v1/orders/{first.OrderId}")).GetProperty("sellerOrders")[0].GetProperty("items")[0].GetProperty("imageUrl");

        var photos = new Dictionary<long, (string Slug, long PhotoId)>();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.ProductMedia.RemoveRange(db.ProductMedia);                                               // the seller replaces the photos of every product
            await db.SaveChangesAsync();
            foreach (var product in await db.Products.ToListAsync())
            {
                var media = new ProductMedia { ProductId = product.Id, FileKey = "photo.jpg", MediaType = "image", SortOrder = 0, ThumbKey = "thumb.webp" };
                db.ProductMedia.Add(media);
                await db.SaveChangesAsync();
                photos[product.Id] = (product.Slug, media.Id);
            }
        }
        var second = await SellerOrderHttpTests.PlaceAsync(retailer);                            // paid, so the seller sees it too

        var oldItem = (await GetAsync(retailer, $"/api/v1/orders/{first.OrderId}")).GetProperty("sellerOrders")[0].GetProperty("items")[0];
        var newItem = (await GetAsync(retailer, $"/api/v1/orders/{second.OrderId}")).GetProperty("sellerOrders")[0].GetProperty("items")[0];
        Assert.Equal(firstUrl.ValueKind == JsonValueKind.Null ? null : firstUrl.GetString(), oldItem.GetProperty("imageUrl").ValueKind == JsonValueKind.Null ? null : oldItem.GetProperty("imageUrl").GetString()); // the old order keeps what it showed
        var (slug, photoId) = photos[newItem.GetProperty("productId").GetInt64()];
        Assert.Equal($"/api/v1/products/{slug}/media/{photoId}?size=thumb", newItem.GetProperty("imageUrl").GetString());
        Assert.NotEqual(firstUrl.ValueKind == JsonValueKind.Null ? null : firstUrl.GetString(), newItem.GetProperty("imageUrl").GetString());
        var sellerView = await GetAsync(seller, "/api/v1/seller/orders?pageSize=50");
        Assert.Contains(sellerView.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetInt64() == second.SellerOrderId);
    }

    [Fact]
    public async Task A_credit_note_can_be_downloaded_by_the_two_businesses_of_the_order_and_by_nobody_elseAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var paid = await SellerOrderHttpTests.PlaceAsync(retailer);
        await MakeInvoiceableAsync(fixture, paid.SellerOrderId);
        Assert.Equal(HttpStatusCode.OK, (await retailer.GetAsync($"/api/v1/invoices/seller-orders/{paid.SellerOrderId}/download")).StatusCode);   // the tax invoice exists
        long processed, requested;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            var one = new TextileConnect.Domain.Payments.Refund { PaymentId = 1, SellerOrderId = paid.SellerOrderId, Amount = 100m, Reason = "Damaged", Status = TextileConnect.Domain.RefundStatus.Processed };
            var two = new TextileConnect.Domain.Payments.Refund { PaymentId = 1, SellerOrderId = paid.SellerOrderId, Amount = 50m, Reason = "Late", Status = TextileConnect.Domain.RefundStatus.Requested };
            db.Refunds.AddRange(one, two);
            await db.SaveChangesAsync();
            processed = one.Id; requested = two.Id;
        }

        var forRetailer = await retailer.GetAsync($"/api/v1/invoices/refunds/{processed}/credit-note");
        Assert.Equal(HttpStatusCode.OK, forRetailer.StatusCode);
        Assert.Equal("application/pdf", forRetailer.Content.Headers.ContentType!.MediaType);
        Assert.Matches(@"^C\d+-\d{4}-00001\.pdf$", forRetailer.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"/api/v1/invoices/refunds/{processed}/credit-note")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await retailer.GetAsync($"/api/v1/invoices/refunds/{requested}/credit-note")).StatusCode);    // not refunded yet
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/invoices/refunds/{processed}/credit-note")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await retailer.GetAsync("/api/v1/invoices/refunds/99999/credit-note")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/invoices/refunds/{processed}/credit-note")).StatusCode);
    }

    /// <summary>Gives the seller of an order what a tax invoice needs (a valid GSTIN and a street address) and marks the order dispatched.</summary>
    internal static async Task MakeInvoiceableAsync(MarketplaceHttpFixture fixture, long sellerOrderId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TextileConnect.Infrastructure.Persistence.MarketplaceDbContext>();
        var sellerOrder = await db.SellerOrders.SingleAsync(x => x.Id == sellerOrderId);
        var seller = await db.Businesses.SingleAsync(x => x.Id == sellerOrder.ManufacturerBusinessId);
        var body = "08AAAAA0000A1Z";
        seller.Gstin = body + TextileConnect.Application.Businesses.GstinRules.CheckCharacter(body + "0");
        var cityId = await db.Cities.Select(x => x.Id).FirstAsync();
        db.BusinessAddresses.RemoveRange(db.BusinessAddresses.Where(x => x.BusinessId == seller.Id && x.AddressType == TextileConnect.Domain.Businesses.AddressType.Registered));
        db.BusinessAddresses.Add(new TextileConnect.Domain.Businesses.BusinessAddress { BusinessId = seller.Id, AddressType = TextileConnect.Domain.Businesses.AddressType.Registered, Line1 = "1 Mill Road", CityId = cityId, Pincode = "302001", IsDefault = true });
        sellerOrder.Status = TextileConnect.Domain.SellerOrderStatus.Dispatched;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Invoice_list_shows_paid_orders_and_the_commission_invoice_stays_with_the_sellerAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var paid = await SellerOrderHttpTests.PlaceAsync(retailer);
        await SellerOrderHttpTests.PlaceAsync(retailer, pay: false);
        var rejected = await SellerOrderHttpTests.PlaceAsync(retailer);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{rejected.SellerOrderId}/reject", new { reason = "No stock" });

        var mine = await GetAsync(retailer, "/api/v1/invoices");
        Assert.Equal(1, mine.GetProperty("total").GetInt32()); // not the unpaid or the refunded order
        var row = mine.GetProperty("items")[0];
        Assert.Equal(paid.SellerOrderId, row.GetProperty("sellerOrderId").GetInt64());
        Assert.Equal("Kapoor Knits", row.GetProperty("counterpartyName").GetString());
        Assert.Equal(31440m, row.GetProperty("amount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("invoiceNumber").ValueKind);
        Assert.Equal("Retailer", (await GetAsync(seller, "/api/v1/invoices")).GetProperty("items")[0].GetProperty("counterpartyName").GetString());

        var url = row.GetProperty("downloadUrl").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await retailer.GetAsync(url)).StatusCode); // nothing to invoice before the goods are dispatched
        await MakeInvoiceableAsync(fixture, paid.SellerOrderId);
        var pdf = await retailer.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Matches(@"^\d+/\d{4}/00001$", (await GetAsync(retailer, "/api/v1/invoices")).GetProperty("items")[0].GetProperty("invoiceNumber").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await retailer.GetAsync(url + "?type=PlatformCommission")).StatusCode); // platform to seller only
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync(url + "?type=PlatformCommission")).StatusCode);
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(0, (await GetAsync(other, "/api/v1/invoices")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await retailer.GetAsync("/api/v1/invoices?page=0")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/invoices")).StatusCode);
    }
}
