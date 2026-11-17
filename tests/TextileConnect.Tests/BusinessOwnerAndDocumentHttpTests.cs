using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TextileConnect.Tests;

public sealed class BusinessOwnerAndDocumentHttpTests
{
    [Fact]
    public async Task Owner_name_is_saved_trimmed_returned_in_profile_and_admin_review_and_length_checkedAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        using var admin = await fixture.ClientAsync("Admin", "admin");

        var tooLong = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", ownerName = new string('a', 121) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var created = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", ownerName = "  Ramesh Sharma  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var profile = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ramesh Sharma", profile.GetProperty("ownerName").GetString());
        Assert.Equal("Ramesh Sharma", (await owner.GetFromJsonAsync<JsonElement>("/api/v1/businesses/me")).GetProperty("ownerName").GetString());

        var id = profile.GetProperty("id").GetInt64();
        var review = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/businesses/{id}");
        Assert.Equal("Ramesh Sharma", review.GetProperty("business").GetProperty("ownerName").GetString());
    }

    [Fact]
    public async Task Owner_name_is_optionalAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        var created = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ownerName").ValueKind);
    }

    [Fact]
    public async Task Document_upload_takes_docType_as_a_query_value_and_the_admin_review_lists_itAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        using var admin = await fixture.ClientAsync("Admin", "admin");
        var id = (await (await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        static MultipartFormDataContent Form() => new() { { new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 gst certificate")), "file", "gst.pdf" } };

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/v1/businesses/me/documents?docType=Passport", Form())).StatusCode);
        var ok = await owner.PostAsync("/api/v1/businesses/me/documents?docType=gst", Form());
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var review = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/businesses/{id}");
        var document = Assert.Single(review.GetProperty("documents").EnumerateArray());
        Assert.Equal("Gst", document.GetProperty("documentType").GetString());
        Assert.Equal("%PDF-1.4 gst certificate", await admin.GetStringAsync(document.GetProperty("url").GetString()!));
    }
}
