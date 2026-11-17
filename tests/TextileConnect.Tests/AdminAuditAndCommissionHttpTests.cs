using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The admin routes to read the audit log and to set the commission rate of one seller.</summary>
public sealed class AdminAuditAndCommissionHttpTests
{
    private static async Task<(MarketplaceHttpFixture Fixture, long SellerId, long RetailerId)> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAdminListEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var seller = new Business { BusinessType = BusinessType.Manufacturer, LegalName = "Seller", DisplayName = "Seller", Phone = "+919999900001" };
        var retailer = new Business { BusinessType = BusinessType.Retailer, LegalName = "Shop", DisplayName = "Shop", Phone = "+919999900002" };
        db.Businesses.AddRange(seller, retailer);
        var start = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 25; i++)
            db.AuditLogs.Add(new AuditLog { Action = i % 5 == 0 ? "settlement.released" : "payment.paid", EntityName = i % 5 == 0 ? "Settlement" : "Payment", EntityId = i.ToString(), UserId = i % 2 == 0 ? "system" : "admin-user", CreatedAtUtc = start.AddDays(i) });
        await db.SaveChangesAsync();
        return (fixture, seller.Id, retailer.Id);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task The_audit_log_is_newest_first_and_paged()
    {
        var (fixture, _, _) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var first = await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?pageSize=10"));
        Assert.Equal(25, first.GetProperty("total").GetInt32());
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        Assert.Equal("24", first.GetProperty("items")[0].GetProperty("entityId").GetString());      // the latest event first
        var last = await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?pageSize=10&page=3"));
        Assert.Equal(5, last.GetProperty("items").GetArrayLength());
        Assert.Equal("0", last.GetProperty("items")[4].GetProperty("entityId").GetString());
    }

    [Fact]
    public async Task The_audit_log_can_be_filtered_by_action_entity_user_and_dates()
    {
        var (fixture, _, _) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");

        Assert.Equal(5, (await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?action=settlement."))).GetProperty("total").GetInt32());
        Assert.Equal(20, (await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?entity=Payment"))).GetProperty("total").GetInt32());
        Assert.Equal(12, (await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?userId=admin-user"))).GetProperty("total").GetInt32());
        var days = await JsonAsync(await admin.GetAsync("/api/v1/admin/audit-log?from=2026-11-03&to=2026-11-05"));
        Assert.Equal(3, days.GetProperty("total").GetInt32());                                      // 3, 4 and 5 November, both ends included
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("from=not-a-date")]
    [InlineData("to=2026-13-45")]
    public async Task A_bad_filter_is_a_bad_request(string query)
    {
        var (fixture, _, _) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit-log?" + query)).StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_reads_the_log_or_sets_a_rate()
    {
        var (fixture, sellerId, _) = await StartAsync();
        await using var _ = fixture; using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 31); using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/admin/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.PutAsJsonAsync($"/api/v1/admin/sellers/{sellerId}/commission-rate", new { rate = 0.5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/audit-log")).StatusCode);
    }

    [Fact]
    public async Task A_seller_rate_is_saved_written_to_the_log_and_can_go_back_to_the_default()
    {
        var (fixture, sellerId, _) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var set = await JsonAsync(await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{sellerId}/commission-rate", new { rate = 0.035m }));
        Assert.Equal(0.035m, set.GetProperty("rate").GetDecimal());
        Assert.False(set.GetProperty("usesDefault").GetBoolean());

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            Assert.Equal(0.035m, (await db.Businesses.SingleAsync(x => x.Id == sellerId)).CommissionRateOverride);
            var entry = await db.AuditLogs.SingleAsync(x => x.Action == "seller.commission-rate");
            Assert.Equal(sellerId.ToString(), entry.EntityId);
            Assert.Equal("default -> 0.035", entry.Details);
        }

        var reset = await JsonAsync(await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{sellerId}/commission-rate", new { rate = (decimal?)null }));
        Assert.True(reset.GetProperty("usesDefault").GetBoolean());
        using var again = fixture.Services.CreateScope();
        Assert.Null((await again.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Businesses.SingleAsync(x => x.Id == sellerId)).CommissionRateOverride);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task A_rate_outside_zero_to_one_is_refused_and_changes_nothing(double rate)
    {
        var (fixture, sellerId, _) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{sellerId}/commission-rate", new { rate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("commission_rate_invalid", (await JsonAsync(response)).GetProperty("code").GetString());
        using var scope = fixture.Services.CreateScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Businesses.SingleAsync(x => x.Id == sellerId)).CommissionRateOverride);
    }

    [Fact]
    public async Task A_retailer_or_a_missing_business_has_no_commission_rate()
    {
        var (fixture, _, retailerId) = await StartAsync();
        await using var _ = fixture; using var admin = await fixture.ClientAsync("Admin", "admin-user");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/admin/sellers/{retailerId}/commission-rate", new { rate = 0.03 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/admin/sellers/99999/commission-rate", new { rate = 0.03 })).StatusCode);
    }
}
