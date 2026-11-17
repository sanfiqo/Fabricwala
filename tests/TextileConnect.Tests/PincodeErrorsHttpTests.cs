using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Wherever an address is saved, a pincode that is wrong or not in the postal directory gets the same clear answer.</summary>
public sealed class PincodeErrorsHttpTests
{
    private static async Task<(string Code, string Message)> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("code").GetString()!, body.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task Sign_up_says_which_pincode_it_could_not_find_and_creates_nothingAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAccountEndpoints());
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        var (code, message) = await ErrorAsync(await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", pincode = "999999" }));
        Assert.Equal("pincode_unknown", code);
        Assert.Contains("999999", message);
        using var scope = fixture.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().Businesses.ToListAsync());
    }

    [Theory]
    [InlineData("999999", "pincode_unknown")]
    [InlineData("12", "pincode_invalid")]
    [InlineData("0123456", "pincode_invalid")]
    [InlineData("", "pincode_invalid")]
    public async Task Delivery_and_pickup_addresses_get_a_pincode_specific_errorAsync(string pincode, string expected)
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAccountEndpoints());
        using var shop = await FullJourneyHttpTests_NewShopAsync(fixture, "Retailer", "shop-1");
        using var factory = await FullJourneyHttpTests_NewShopAsync(fixture, "Manufacturer", "factory-1");
        foreach (var client in new[] { shop, factory })
        {
            Assert.Equal(expected, (await ErrorAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Road 1", pincode }))).Code);
            var created = await client.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "Road 1", pincode = "452001" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            Assert.Equal(expected, (await ErrorAsync(await client.PutAsJsonAsync($"/api/v1/account/addresses/{id}", new { line1 = "Road 2", pincode }))).Code);
        }
    }

    [Fact]
    public async Task A_bad_street_line_is_still_a_plain_400_and_the_business_address_book_takes_the_city_from_the_pincodeAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapAccountEndpoints());
        using var shop = await FullJourneyHttpTests_NewShopAsync(fixture, "Retailer", "shop-1");
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostAsJsonAsync("/api/v1/account/addresses", new { line1 = "", pincode = "452001" })).StatusCode);

        // The old address book route used to trust a cityId sent by the caller. Now the city is the one of the pincode, and an unknown pincode is refused.
        Assert.Equal("pincode_unknown", (await ErrorAsync(await shop.PostAsJsonAsync("/api/v1/businesses/me/addresses", new { addressType = "Delivery", line1 = "Road 1", pincode = "999999", cityId = 1 }))).Code);
        var ok = await shop.PostAsJsonAsync("/api/v1/businesses/me/addresses", new { addressType = "Delivery", line1 = "Road 1", pincode = "452001", cityId = 99999, businessId = 123456 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var address = await ok.Content.ReadFromJsonAsync<JsonElement>();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var indore = await db.Cities.AsNoTracking().SingleAsync(x => x.Name == "Indore");
        Assert.Equal(indore.Id, address.GetProperty("cityId").GetInt64());
        Assert.NotEqual(123456, address.GetProperty("businessId").GetInt64());
    }

    private static async Task<HttpClient> FullJourneyHttpTests_NewShopAsync(MarketplaceHttpFixture fixture, string type, string user)
    {
        var first = await fixture.ClientAsync(type, user);
        var id = await FullJourneyHttpTests.SignUpAsync(first, type, user + " business", "452001");
        first.Dispose();
        var admin = await fixture.ClientAsync("Admin", "admin-" + user);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/admin/businesses/{id}/approve", null)).StatusCode);
        return await fixture.ClientAsync(type, user, id);
    }
}
