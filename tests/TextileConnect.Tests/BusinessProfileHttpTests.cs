using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class BusinessProfileHttpTests
{
    [Theory]
    [InlineData("Retailer")]
    [InlineData("Manufacturer")]
    public async Task Signup_ignores_privileged_fields_and_profile_is_isolatedAsync(string type)
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        using var other = await fixture.ClientAsync("Retailer", "other");
        using var admin = await fixture.ClientAsync("Admin", "admin");
        using var anonymous = await fixture.ClientAsync(null);
        var request = new { businessType = type, legalName = "Sharma Textiles", displayName = "Sharma", pincode = "452001", gstin = "", id = 998, verificationStatus = "Approved", isPlatformOwned = true, commissionRateOverride = 0.99 };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/businesses", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/businesses", request)).StatusCode);
        var response = await owner.PostAsJsonAsync("/api/v1/businesses", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", created.GetProperty("verificationStatus").GetString());
        Assert.Equal("Indore", created.GetProperty("city").GetString());
        Assert.Equal("Madhya Pradesh", created.GetProperty("state").GetString());
        Assert.False(created.TryGetProperty("pan", out _));
        Assert.NotEqual(998, created.GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/businesses/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/businesses", request)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var business = await db.Businesses.SingleAsync();
        Assert.False(business.IsPlatformOwned);
        Assert.Null(business.CommissionRateOverride);
        Assert.Null(business.Gstin);
        business.VerificationStatus = VerificationStatus.Rejected;
        business.RejectionNote = "Please update the document.";
        await db.SaveChangesAsync();
        var profile = await owner.GetFromJsonAsync<JsonElement>("/api/v1/businesses/me");
        Assert.Equal("Rejected", profile.GetProperty("verificationStatus").GetString());
        Assert.Equal(business.RejectionNote, profile.GetProperty("rejectionNote").GetString());
    }

    [Theory]
    [InlineData("bad-gst", "452001", 400)]
    [InlineData(null, "123", 400)]
    [InlineData(null, "999999", 404)]
    public async Task Invalid_registration_does_not_create_business_or_membershipAsync(string? gstin, string pincode, int status)
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer");
        var response = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", gstin, pincode });
        Assert.Equal(status, (int)response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Empty(await db.Businesses.ToListAsync());
        Assert.Empty(await db.BusinessUsers.ToListAsync());
    }

    [Fact]
    public async Task Pincode_lookup_is_exact_and_gst_is_optional_but_normalizedAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer");
        var location = await owner.GetFromJsonAsync<PincodeResponse>("/api/v1/pincodes/141001");
        Assert.Equal("Ludhiana", location!.City);
        Assert.Equal("Punjab", location.State);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/v1/pincodes/141999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/v1/pincodes/000000")).StatusCode);
        var response = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", gstin = "27abcde1234f1z0" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("27ABCDE1234F1Z0", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("gstin").GetString());
    }
}
