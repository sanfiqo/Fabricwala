using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class ReturnClaimHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Retailer, HttpClient Seller)> StartAsync()
    {
        var fixture = await PaymentHttpTests.StartAsync(app => app.MapReturnClaimEndpoints());
        return (fixture, await fixture.ClientAsync("Retailer", "retailer-user", 30), await fixture.ClientAsync("Manufacturer", "seller-user", 10));
    }

    private static async Task ShipAsync(HttpClient seller, long sellerOrderId)
    {
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-900" });
    }

    private static Task<HttpResponseMessage> ReportAsync(HttpClient client, long sellerOrderId, object body)
        => client.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", body);

    private static Task<HttpResponseMessage> PhotoAsync(HttpClient client, long sellerOrderId, long claimId, string contentType = "image/png", int? size = null)
    {
        // a real photo unless a size is asked for: zeros of that size are not an image, whatever the declared type says
        var file = new ByteArrayContent(size is null ? ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Png, 40, 30, SkiaSharp.SKColors.Red) : new byte[size.Value]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return client.PostAsync($"/api/v1/seller-orders/{sellerOrderId}/claims/{claimId}/photos", new MultipartFormDataContent { { file, "file", "photo.png" } });
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    [Fact]
    public async Task Claim_needs_a_shipped_order_valid_fields_and_only_one_open_reportAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (_, id) = await SellerOrderHttpTests.PlaceAsync(retailer); // 60 pieces ordered
        var good = new { reason = "Damaged", pieces = 5, note = "Torn seams" };
        var early = await ReportAsync(retailer, id, good);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("claim_not_allowed", await CodeAsync(early));
        await ShipAsync(seller, id);

        foreach (var bad in new object[] { new { reason = "Damaged", pieces = 0 }, new { reason = "Damaged", pieces = 61 }, new { reason = 99, pieces = 1 },
                     new { reason = "Other", pieces = 1 }, new { reason = "Damaged", pieces = 1, note = new string('x', 1001) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await ReportAsync(retailer, id, bad)).StatusCode);

        var created = await ReportAsync(retailer, id, good);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var claim = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Damaged", claim.GetProperty("reason").GetString());
        Assert.Equal(5, claim.GetProperty("pieces").GetInt32());
        Assert.Equal("Torn seams", claim.GetProperty("note").GetString());
        Assert.Equal("Submitted", claim.GetProperty("status").GetString());
        Assert.True(claim.GetProperty("paymentOnHold").GetBoolean());
        var again = await ReportAsync(retailer, id, good);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("claim_already_open", await CodeAsync(again));
        var list = await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/seller-orders/{id}/claims");
        Assert.Equal(1, list.GetArrayLength());

        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(other, id, good)).StatusCode);
        Assert.Equal(0, (await other.GetFromJsonAsync<JsonElement>($"/api/v1/seller-orders/{id}/claims")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await ReportAsync(seller, id, good)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Notifications.CountAsync(x => x.UserId == "seller-user" && x.Title == "Problem reported"));
    }

    [Fact]
    public async Task Claim_accepts_at_most_three_valid_photosAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        var (_, id) = await SellerOrderHttpTests.PlaceAsync(retailer);
        await ShipAsync(seller, id);
        var claimId = (await (await ReportAsync(retailer, id, new { reason = "WrongItem", pieces = 2 })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await PhotoAsync(retailer, id, claimId, "application/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PhotoAsync(retailer, id, claimId, size: 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PhotoAsync(retailer, id, claimId, size: 5 * 1024 * 1024 + 1)).StatusCode);
        var notAnImage = await PhotoAsync(retailer, id, claimId, size: 64);   // the declared type is image/png but the bytes are zeros
        Assert.Equal(HttpStatusCode.BadRequest, notAnImage.StatusCode);
        Assert.Equal("photo_invalid_type", await CodeAsync(notAnImage));
        for (var i = 1; i <= 3; i++)
            Assert.Equal(i, (await (await PhotoAsync(retailer, id, claimId)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photoCount").GetInt32());
        var fourth = await PhotoAsync(retailer, id, claimId);
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        Assert.Equal("claim_photo_limit", await CodeAsync(fourth));
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await PhotoAsync(other, id, claimId)).StatusCode);
        Assert.Equal(3, (await retailer.GetFromJsonAsync<JsonElement>($"/api/v1/seller-orders/{id}/claims"))[0].GetProperty("photoCount").GetInt32());
    }

    [Fact]
    public async Task Open_claim_holds_the_payout_and_the_decision_releases_it_minus_the_refundAsync()
    {
        var (fixture, retailer, seller) = await StartAsync();
        await using var _ = fixture; using var __ = retailer; using var ___ = seller;
        using var admin = await fixture.ClientAsync("Admin", "admin-user");
        var (orderId, id) = await SellerOrderHttpTests.PlaceAsync(retailer);
        await ShipAsync(seller, id);
        var claimId = (await (await ReportAsync(retailer, id, new { reason = "Damaged", pieces = 10, note = "Wet in transit" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        async Task RunJobAsync()
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IOrderMaintenance>().ExpireAsync();
        }
        async Task<string> SettlementStatusAsync()
        {
            using var scope = fixture.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Settlements.SingleAsync(x => x.SellerOrderId == id)).Status;
        }
        fixture.Clock.Advance(TimeSpan.FromDays(10));
        await RunJobAsync();
        Assert.Equal("Pending", await SettlementStatusAsync()); // delivered and past the 7 days, still held because of the claim

        using var other = await fixture.ClientAsync("Manufacturer", "other-seller", 12);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/seller/claims/{claimId}/respond", new { response = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync($"/api/v1/seller/claims/{claimId}/respond", new { response = " " })).StatusCode);
        var responded = await PaymentHttpTests.JsonAsync(await seller.PostAsJsonAsync($"/api/v1/seller/claims/{claimId}/respond", new { response = "We will check" }));
        Assert.Equal("SellerResponded", responded.GetProperty("status").GetString());
        Assert.Equal("claim_not_open", await CodeAsync(await seller.PostAsJsonAsync($"/api/v1/seller/claims/{claimId}/respond", new { response = "again" })));

        foreach (var bad in new object[] { new { decision = "Approved", refundAmount = 0, note = "ok" }, new { decision = "Approved", refundAmount = 99999, note = "ok" },
                     new { decision = "Approved", refundAmount = 10.005, note = "ok" }, new { decision = "Approved", refundAmount = 100, note = " " }, new { decision = "Maybe", refundAmount = 100, note = "ok" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "Rejected", refundAmount = 0, note = "x" })).StatusCode);
        var decided = await PaymentHttpTests.JsonAsync(await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "PartiallyRefunded", refundAmount = 1000, note = "Part damaged" }));
        Assert.False(decided.GetProperty("paymentOnHold").GetBoolean());
        Assert.Equal(1000m, decided.GetProperty("approvedRefundAmount").GetDecimal());
        Assert.Equal("claim_not_open", await CodeAsync(await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "Rejected", refundAmount = 0, note = "late" })));

        await RunJobAsync();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var settlement = await db.Settlements.SingleAsync(x => x.SellerOrderId == id);
        Assert.Equal("Released", settlement.Status);
        Assert.Equal(31440m - 576m - 1000m, settlement.Amount);
        var refund = await db.Refunds.SingleAsync(x => x.SellerOrderId == id);
        Assert.Equal(1000m, refund.Amount);
        Assert.Equal("Processed", refund.Status);
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "retailer-user" && x.Title == "Problem report decided"));
    }
}
