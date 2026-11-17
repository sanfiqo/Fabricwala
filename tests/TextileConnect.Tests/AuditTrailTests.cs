using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Who did what, when and from which IP, for every approval, moderation, configuration and money action.</summary>
public sealed class AuditTrailTests
{
    private static async Task<List<AuditLog>> RowsAsync(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().AuditLogs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    }

    private static void AssertFromARealCaller(AuditLog row, string user)
    {
        Assert.Equal(user, row.UserId);
        Assert.True(System.Net.IPAddress.TryParse(row.IpAddress, out _), $"'{row.Action}' has no IP address");
        Assert.True(row.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    // ---------------------------------------------------------------- approvals

    [Fact]
    public async Task Business_approval_and_rejection_are_recorded_with_the_admin_the_business_and_the_ipAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        using var admin = await fixture.ClientAsync("Admin", "admin-anita");
        using var shop1 = await fixture.ClientAsync("Retailer", "shop-1");
        using var shop2 = await fixture.ClientAsync("Retailer", "shop-2");
        var first = await FullJourneyHttpTests.SignUpAsync(shop1, "Retailer", "First Shop", "452001");
        var second = await FullJourneyHttpTests.SignUpAsync(shop2, "Retailer", "Second Shop", "452001");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/admin/businesses/{first}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/businesses/{second}/reject", new { note = "TOP-SECRET-NOTE documents unreadable" })).StatusCode);

        var rows = await RowsAsync(fixture);
        var approve = Assert.Single(rows, x => x.Action == "POST businesses/{id:long}/approve");
        AssertFromARealCaller(approve, "admin-anita");
        Assert.Equal("businesses", approve.EntityName);
        Assert.Equal(first.ToString(), approve.EntityId);
        var reject = Assert.Single(rows, x => x.Action == "POST businesses/{id:long}/reject");
        AssertFromARealCaller(reject, "admin-anita");
        Assert.Equal(second.ToString(), reject.EntityId);
        // The note the admin typed is private: no column of any row holds a request body.
        Assert.DoesNotContain(rows, x => JsonSerializer.Serialize(x).Contains("TOP-SECRET-NOTE"));
    }

    // ---------------------------------------------------------------- moderation and configuration

    [Fact]
    public async Task Moderation_suspension_catalogue_and_settings_changes_are_recorded_and_failed_or_foreign_calls_are_notAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app =>
        {
            app.MapAdminEndpoints(); app.MapProductEndpoints(); app.MapProductModerationEndpoints(); app.MapBusinessSuspensionEndpoints(); app.MapCategoryBannerEndpoints();
        });
        await CatalogueHttpTests.SeedAsync(fixture);
        using var admin = await fixture.ClientAsync("Admin", "admin-anita");
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/products/1/moderate", new { action = "Deactivate", reason = "Fake brand" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/businesses/10/suspension", new { action = "Suspend" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/admin/settings", new { commissionDefaultRate = "0.03", settlementAutoReleaseDays = "10" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/catalogue/categories", new { name = "Shawls", slug = "shawls", sortOrder = 9, isActive = true })).StatusCode);

        // None of these may leave a row: a refused request, an unknown product, a read, and a call by someone who is not an admin.
        Assert.NotEqual(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/products/999/moderate", new { action = "Deactivate", reason = "x" })).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/businesses/10/suspension", new { action = "Explode" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PutAsJsonAsync("/api/v1/admin/settings", new { commissionDefaultRate = "0.99", settlementAutoReleaseDays = "1" })).StatusCode);

        var rows = await RowsAsync(fixture);
        Assert.Equal(4 + 2, rows.Count(x => x.Action is not null)); // 4 admin writes, plus 2 rows that carry the details below
        Assert.All(rows, x => AssertFromARealCaller(x, "admin-anita"));

        var moderation = rows.Single(x => x.Action == "product.deactivate");
        Assert.Equal("1", moderation.EntityId);
        Assert.Contains(rows, x => x.Action == "POST products/{id:long}/moderate" && x.EntityId == "1");
        Assert.Contains(rows, x => x.Action == "POST businesses/{id:long}/suspension" && x.EntityId == "10");
        var settings = rows.Single(x => x.Action == "settings.updated");
        Assert.Equal("Commission.DefaultRate=0.03; Settlement.AutoReleaseDays=10", settings.Details); // the new money rules, written down
        Assert.Contains(rows, x => x.Action == "PUT settings");
        Assert.Contains(rows, x => x.Action == "POST catalogue/categories");
    }

    // ---------------------------------------------------------------- money

    [Fact]
    public async Task Payment_order_moves_and_payout_release_are_recorded_by_who_did_themAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, _, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (orderId, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "audit-money-1", pay: true);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-9" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await retailer.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TextileConnect.Application.Orders.IOrderMaintenance>().ExpireAsync(); // the per-minute job

        var rows = await RowsAsync(fixture);
        AssertFromARealCaller(rows.Single(x => x.Action == "payment.paid"), "journey-retailer");
        Assert.Contains("amount 52400.00", rows.Single(x => x.Action == "payment.paid").Details);
        AssertFromARealCaller(rows.Single(x => x.Action == "order.accepted"), "journey-seller");
        AssertFromARealCaller(rows.Single(x => x.Action == "order.dispatched"), "journey-seller");
        var delivered = rows.Single(x => x.Action == "order.delivered");
        AssertFromARealCaller(delivered, "journey-retailer");
        Assert.Equal(sellerOrderId.ToString(), delivered.EntityId);

        var released = rows.Single(x => x.Action == "settlement.released");
        Assert.Equal("system", released.UserId);          // a background job has no user and no IP
        Assert.Null(released.IpAddress);
        Assert.Equal("amount 51440.00", released.Details);   // 52,400 minus 2% commission on 48,000
    }

    [Fact]
    public async Task A_rejected_order_records_the_rejection_the_refund_request_and_the_refund_paymentAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, _, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (_, sellerOrderId, total) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "audit-reject-1", pay: true);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/reject", new { reason = "Out of fabric" })).StatusCode);

        var rows = await RowsAsync(fixture);
        var rejected = rows.Single(x => x.Action == "order.rejected");
        AssertFromARealCaller(rejected, "journey-seller");
        Assert.Contains("Out of fabric", rejected.Details);
        Assert.Equal($"amount {total:0.00}", rows.Single(x => x.Action == "refund.requested").Details);
        Assert.Equal($"amount {total:0.00}", rows.Single(x => x.Action == "refund.processed").Details);
    }

    [Fact]
    public async Task A_claim_decision_records_the_decision_and_the_refund_amountAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync();
        var (retailer, seller, admin, _, _, variantId, _, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);
        var (_, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailer, variantId, addressId, "audit-claim-1", pay: true);
        await seller.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await seller.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-10" });
        var claimId = (await (await retailer.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", new { reason = "Damaged", pieces = 5, note = "Wet" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/claims/{claimId}/decide", new { decision = "PartiallyRefunded", refundAmount = 1200m, note = "Part damaged" })).StatusCode);

        var decided = (await RowsAsync(fixture)).Single(x => x.Action == "claim.decided");
        AssertFromARealCaller(decided, "journey-admin");
        Assert.Equal(claimId.ToString(), decided.EntityId);
        Assert.Equal("PartiallyRefunded, refund 1200.00", decided.Details);
    }

    [Fact]
    public async Task A_payment_confirmed_by_the_gateway_webhook_is_recorded_as_anonymous_with_the_callers_ipAsync()
    {
        await WebhookEventHttpTests.PaidByWebhookAndAuditedAsync();
    }
}
