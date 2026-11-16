using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class AccountHttpTests
{
    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAccountEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.Businesses.AddRange(
            new Business { Id = 30, BusinessType = BusinessType.Retailer, LegalName = "Sharma Textiles", DisplayName = "Sharma Textiles", Phone = "+919000000001", VerificationStatus = VerificationStatus.Approved },
            new Business { Id = 31, BusinessType = BusinessType.Retailer, LegalName = "Other", DisplayName = "Other", Phone = "+919000000031", VerificationStatus = VerificationStatus.Approved },
            new Business { Id = 10, BusinessType = BusinessType.Manufacturer, LegalName = "Kapoor Knits", DisplayName = "Kapoor Knits", Phone = "+919000000002", VerificationStatus = VerificationStatus.Approved });
        await db.SaveChangesAsync();
        return fixture;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Profile_and_language_can_be_read_and_changed_with_validationAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        var account = await BodyAsync(await client.GetAsync("/api/v1/account"));
        Assert.Equal("en", account.GetProperty("language").GetString());
        Assert.Equal("Retailer", account.GetProperty("roles")[0].GetString());
        Assert.Equal("Sharma Textiles", account.GetProperty("business").GetProperty("displayName").GetString());

        Assert.Equal("hi", (await BodyAsync(await client.PutAsJsonAsync("/api/v1/account/language", new { language = "hi" }))).GetProperty("language").GetString());
        var renamed = await BodyAsync(await client.PutAsJsonAsync("/api/v1/account", new { fullName = "  Rohan Sharma  " }));
        Assert.Equal("Rohan Sharma", renamed.GetProperty("fullName").GetString());
        Assert.Equal("hi", renamed.GetProperty("language").GetString()); // untouched by a name change
        Assert.Equal("hi", (await BodyAsync(await client.GetAsync("/api/v1/account"))).GetProperty("language").GetString());
        foreach (var bad in new object[] { new { language = "fr" }, new { language = "" }, new { fullName = new string('x', 101) }, new { } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/account", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/account/language", new { language = "de" })).StatusCode);

        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/account/language", new { language = "hi" })).StatusCode);
    }

    [Fact]
    public async Task Addresses_add_edit_default_delete_use_the_postal_directory_and_stay_privateAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        Assert.Empty((await BodyAsync(await client.GetAsync("/api/v1/account/addresses"))).EnumerateArray());

        var first = await BodyAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = " 12 Market Road ", line2 = "Near temple", pincode = "452001" }), HttpStatusCode.Created);
        Assert.Equal("Delivery", first.GetProperty("type").GetString());
        Assert.Equal("12 Market Road", first.GetProperty("line1").GetString());
        Assert.Equal("Indore", first.GetProperty("city").GetString());
        Assert.Equal("Madhya Pradesh", first.GetProperty("state").GetString());
        Assert.True(first.GetProperty("isDefault").GetBoolean()); // the first address is the default
        var second = await BodyAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Shop 4", pincode = "141001" }), HttpStatusCode.Created);
        Assert.False(second.GetProperty("isDefault").GetBoolean());
        Assert.Equal("Ludhiana", second.GetProperty("city").GetString());
        var firstId = first.GetProperty("id").GetInt64(); var secondId = second.GetProperty("id").GetInt64();

        foreach (var bad in new object[] { new { line1 = "", pincode = "452001" }, new { line1 = "x", pincode = "12" }, new { line1 = "x", pincode = "999999" }, new { line1 = new string('x', 201), pincode = "452001" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/addresses", bad)).StatusCode);

        var edited = await BodyAsync(await client.PutAsJsonAsync($"/api/v1/account/addresses/{secondId}", new { line1 = "Shop 9", pincode = "452001" }));
        Assert.Equal("Indore", edited.GetProperty("city").GetString());
        Assert.Equal("Shop 9", edited.GetProperty("line1").GetString());
        var list = await BodyAsync(await client.PutAsync($"/api/v1/account/addresses/{secondId}/default", null));
        Assert.Equal(secondId, list[0].GetProperty("id").GetInt64()); // default first
        Assert.Single(list.EnumerateArray(), x => x.GetProperty("isDefault").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/account/addresses/{secondId}")).StatusCode); // deleting the default promotes the other
        var after = await BodyAsync(await client.GetAsync("/api/v1/account/addresses"));
        Assert.Equal(1, after.GetArrayLength());
        Assert.Equal(firstId, after[0].GetProperty("id").GetInt64());
        Assert.True(after[0].GetProperty("isDefault").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/account/addresses/{secondId}")).StatusCode);

        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Empty((await BodyAsync(await other.GetAsync("/api/v1/account/addresses"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/account/addresses/{firstId}", new { line1 = "Hijack", pincode = "452001" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/v1/account/addresses/{firstId}/default", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/account/addresses/{firstId}")).StatusCode);
        Assert.Equal(1, (await BodyAsync(await client.GetAsync("/api/v1/account/addresses"))).GetArrayLength());
    }

    [Fact]
    public async Task Sellers_manage_pickup_addresses_separately_and_the_limit_appliesAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 30);
        var pickup = await BodyAsync(await seller.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Factory gate 2", pincode = "141001" }), HttpStatusCode.Created);
        Assert.Equal("Pickup", pickup.GetProperty("type").GetString());
        Assert.Empty((await BodyAsync(await retailer.GetAsync("/api/v1/account/addresses"))).EnumerateArray()); // types and businesses never mix
        for (var i = 1; i < AccountEndpoints.MaxAddresses; i++)
            Assert.Equal(HttpStatusCode.Created, (await seller.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Unit " + i, pincode = "141001" })).StatusCode);
        var over = await seller.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "One too many", pincode = "141001" });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal("address_limit", (await over.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var admin = await fixture.ClientAsync("Admin", "admin-user");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/account/addresses")).StatusCode);
    }
}
