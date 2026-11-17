using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>The admin screen for the platform company, which decides whether the platform fee carries GST on new orders.</summary>
public sealed class PlatformCompanyHttpTests
{
    private static string Gstin()
    {
        var body = "08AAAAA0000A1Z";
        return body + GstinRules.CheckCharacter(body + "0");
    }

    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAdminEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var state = new State { Name = "Rajasthan", Code = "RJ" }; db.States.Add(state); await db.SaveChangesAsync();
        var city = new City { Name = "Jaipur", StateId = state.Id }; db.Cities.Add(city); await db.SaveChangesAsync();
        db.PostalCodes.Add(new PostalCode { Pincode = "302001", CityId = city.Id });
        await db.SaveChangesAsync();
        return fixture;
    }

    private static object Body(string? gstin = null, string pincode = "302001", decimal rate = 18m, string name = "Fabricwala Pvt Ltd")
        => new { legalName = name, gstin = gstin ?? Gstin(), address = "9 Tech Park", pincode, feeGstRate = rate };

    [Fact]
    public async Task The_admin_saves_the_company_and_the_fee_gst_switches_on()
    {
        await using var fixture = await StartAsync();
        using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var before = await (await admin.GetAsync("/api/v1/admin/platform-company")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(before.GetProperty("isComplete").GetBoolean());

        var saved = await admin.PutAsJsonAsync("/api/v1/admin/platform-company", Body(gstin: Gstin().ToLowerInvariant()));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var company = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(company.GetProperty("isComplete").GetBoolean());
        Assert.Equal(Gstin(), company.GetProperty("gstin").GetString());      // stored in capitals
        Assert.Equal(18m, company.GetProperty("feeGstRate").GetDecimal());

        using var scope = fixture.Services.CreateScope();
        Assert.NotNull(await TextileConnect.Infrastructure.Payments.PlatformCompany.LoadAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>()));
    }

    [Theory]
    [InlineData("0", "302001", 18, "Fabricwala", "platform_gstin_invalid")]
    [InlineData(null, "999999", 18, "Fabricwala", "platform_pincode_unknown")]
    [InlineData(null, "302001", 101, "Fabricwala", "platform_rate_invalid")]
    [InlineData(null, "302001", -1, "Fabricwala", "platform_rate_invalid")]
    [InlineData(null, "302001", 18, "", "platform_name_invalid")]
    public async Task Wrong_details_are_refused_with_a_reason_and_nothing_is_saved(string? gstin, string pincode, int rate, string name, string code)
    {
        await using var fixture = await StartAsync();
        using var admin = await fixture.ClientAsync("Admin", "admin-user");

        var response = await admin.PutAsJsonAsync("/api/v1/admin/platform-company", Body(gstin, pincode, rate, name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var scope = fixture.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().PlatformSettings.AnyAsync(x => x.Key.StartsWith("Platform.")));
    }

    [Fact]
    public async Task Only_an_admin_can_read_or_change_the_company()
    {
        await using var fixture = await StartAsync();
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 31);
        using var anonymous = await fixture.ClientAsync(null);

        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.GetAsync("/api/v1/admin/platform-company")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await retailer.PutAsJsonAsync("/api/v1/admin/platform-company", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/admin/platform-company", Body())).StatusCode);
    }
}
