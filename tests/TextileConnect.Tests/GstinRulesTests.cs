using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TextileConnect.Application.Businesses;

namespace TextileConnect.Tests;

public sealed class GstinRulesTests
{
    [Theory]
    [InlineData("27AAPFU0939F1ZV")] // a widely published example GSTIN
    [InlineData("27ABCDE1234F1Z0")]
    [InlineData("03AAACL1234F1Z9")]
    [InlineData("24AABCS5678K1ZY")]
    public void A_gstin_with_the_right_check_character_is_valid(string gstin) => Assert.True(GstinRules.IsValid(gstin));

    [Theory]
    [InlineData("27AAPFU0939F1ZW")] // wrong check character
    [InlineData("27AAPFU0939F1Z5")]
    [InlineData("27AAPFU0939F2ZV")] // one digit changed, so the old check character no longer fits
    [InlineData("39AAPFU0939F1Z5")] // state code 39 does not exist
    [InlineData("00AAPFU0939F1Z5")]
    [InlineData("27aapfu0939f1zv")] // lower case is normalised before it gets here
    [InlineData("27AAPFU0939F1Y5")] // 14th character must be Z
    [InlineData("27AAPFU0939F1Z")]
    [InlineData("")]
    [InlineData(null)]
    public void A_wrong_gstin_is_refused(string? gstin) => Assert.False(GstinRules.IsValid(gstin));

    [Fact]
    public void The_check_character_is_worked_out_from_the_first_fourteen()
    {
        Assert.Equal('V', GstinRules.CheckCharacter("27AAPFU0939F1ZV"));
        Assert.Equal('0', GstinRules.CheckCharacter("27ABCDE1234F1Z0"));
    }

    [Fact]
    public void A_missing_gstin_is_allowed_because_it_is_optional()
    {
        Assert.True(BusinessInputRules.IsGstin(null));
        Assert.True(BusinessInputRules.IsGstin(""));
        Assert.False(BusinessInputRules.IsGstin("27AAPFU0939F1ZW"));
    }

    [Fact]
    public async Task Sign_up_refuses_a_gstin_with_a_wrong_check_character_and_accepts_the_right_oneAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");

        var wrong = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", gstin = "27AAPFU0939F1ZW" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("gstin", (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fields").EnumerateObject().Select(x => x.Name));

        var right = await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop", gstin = "27aapfu0939f1zv" });
        Assert.Equal(HttpStatusCode.Created, right.StatusCode);
        Assert.Equal("27AAPFU0939F1ZV", (await right.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("gstin").GetString());
    }
}
