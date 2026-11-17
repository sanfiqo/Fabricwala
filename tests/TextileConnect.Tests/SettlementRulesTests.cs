using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Domain;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The money rules of docs/SETTLEMENT.md, one test per rule.</summary>
public sealed class SettlementRulesTests
{
    [Theory]
    [InlineData("0.125", "0.13")]   // a half goes away from zero, never to the even neighbour
    [InlineData("0.135", "0.14")]
    [InlineData("2.675", "2.68")]
    [InlineData("0.124", "0.12")]
    [InlineData("333.333", "333.33")]
    [InlineData("6.6666", "6.67")]
    [InlineData("-0.125", "-0.13")]
    public void Money_rounds_to_paise_with_one_rule(string value, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ProductQuote.Money(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Razorpay_amounts_are_whole_paise_rounded_the_same_way()
    {
        Assert.Equal(52400_00L, TextileConnect.Infrastructure.Payments.RazorpayPaymentGateway.Paise(52400m));
        Assert.Equal(1L, TextileConnect.Infrastructure.Payments.RazorpayPaymentGateway.Paise(0.005m));   // a half paise rounds up, as everywhere else
        Assert.Equal(19_99L, TextileConnect.Infrastructure.Payments.RazorpayPaymentGateway.Paise(19.99m)); // no float noise: 19.99 * 100 must not become 1998
    }

    private static async Task<(decimal Total, decimal Commission, decimal SettlementAmount, decimal CommissionRate)> PlaceAsync(
        MarketplaceHttpFixture fixture, HttpClient retailer, long variantId, long addressId, string key)
    {
        var (_, sellerOrderId, total) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, key, pay: true);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var sellerOrder = await db.SellerOrders.AsNoTracking().SingleAsync(x => x.Id == sellerOrderId);
        var settlement = await db.Settlements.AsNoTracking().SingleAsync(x => x.SellerOrderId == sellerOrderId);
        Assert.Equal(sellerOrder.CommissionAmount, settlement.CommissionAmount);
        return (total, sellerOrder.CommissionAmount, settlement.Amount, sellerOrder.CommissionRate);
    }

    [Fact]
    public async Task Commission_is_charged_to_the_seller_on_the_goods_only_and_the_override_beats_the_default_rateAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, _, _, _, sellerId, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);

        // 100 pieces at 480 = 48,000 goods, 5% GST = 2,400, freight = 2,000. The retailer pays 52,400; the platform fee never changes that.
        var first = await PlaceAsync(fixture, retailer, variantId, addressId, "settle-default-1");
        Assert.Equal(52400m, first.Total);
        Assert.Equal(0.02m, first.CommissionRate);              // the platform default
        Assert.Equal(960m, first.Commission);                   // 2% of 48,000, not of GST or freight
        Assert.Equal(52400m - 960m, first.SettlementAmount);    // the seller gets the rest, GST and freight included

        // The admin changes the default: it applies to new orders only.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            (await db.PlatformSettings.SingleAsync(x => x.Key == "Commission.DefaultRate")).Value = "0.05";
            await db.SaveChangesAsync();
        }
        var second = await PlaceAsync(fixture, retailer, variantId, addressId, "settle-default-2");
        Assert.Equal(2400m, second.Commission);
        Assert.Equal(52400m - 2400m, second.SettlementAmount);

        // A seller with their own rate ignores the default.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            (await db.Businesses.SingleAsync(x => x.Id == sellerId)).CommissionRateOverride = 0.035m;
            await db.SaveChangesAsync();
        }
        var third = await PlaceAsync(fixture, retailer, variantId, addressId, "settle-override-3");
        Assert.Equal(0.035m, third.CommissionRate);
        Assert.Equal(1680m, third.Commission);
        Assert.Equal(52400m - 1680m, third.SettlementAmount);

        using var check = fixture.Services.CreateScope();
        var orders = await check.ServiceProvider.GetRequiredService<MarketplaceDbContext>().SellerOrders.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(new[] { 0.02m, 0.05m, 0.035m }, orders.Select(x => x.CommissionRate)); // each order keeps the rate it was placed with
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(14)]
    public async Task The_payout_is_scheduled_for_the_configured_number_of_days_after_dispatchAsync(int days)
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, _, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            (await db.PlatformSettings.SingleAsync(x => x.Key == "Settlement.AutoReleaseDays")).Value = days.ToString();
            await db.SaveChangesAsync();
        }
        var (_, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "settle-days-" + days, pay: true);

        // Before dispatch there is no date: the payout waits for the shipment.
        using (var scope = fixture.Services.CreateScope())
            Assert.False(TextileConnect.Domain.Payments.Settlements.HasReleaseDate((await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Settlements.SingleAsync()).ReleaseAfter));

        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null)).StatusCode);
        var dispatchedAt = fixture.Clock.GetUtcNow().UtcDateTime;
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-77" })).StatusCode);

        using var check = fixture.Services.CreateScope();
        var settlement = await check.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Settlements.SingleAsync();
        Assert.Equal(dispatchedAt.AddDays(days), settlement.ReleaseAfter, TimeSpan.FromSeconds(1));
        Assert.Equal(SettlementStatus.Pending, settlement.Status);
    }

    [Fact]
    public async Task A_claim_refund_comes_out_of_the_held_payout_and_never_makes_it_negativeAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, admin, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, total) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "settle-claim-1", pay: true);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-78" })).StatusCode);
        var claim = await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", new { reason = "Damaged", pieces = 10, note = "Wet" });
        Assert.Equal(HttpStatusCode.Created, claim.StatusCode);
        var claimId = (await claim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        decimal before;
        using (var scope = fixture.Services.CreateScope())
            before = (await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Settlements.SingleAsync()).Amount;

        // The admin refunds 5,000 of the order: the retailer gets it back and the seller's payout is 5,000 smaller.
        var decided = await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "PartiallyRefunded", refundAmount = 5000m, note = "Part of the goods were wet" });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            Assert.Equal(before - 5000m, (await db.Settlements.SingleAsync()).Amount);
            Assert.Equal(5000m, (await db.Refunds.SingleAsync(x => x.SellerOrderId == sellerOrderId)).Amount);
        }
        Assert.True(total >= before);
    }
}
