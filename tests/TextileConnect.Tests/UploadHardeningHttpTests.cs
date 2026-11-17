using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using TextileConnect.Api;

namespace TextileConnect.Tests;

/// <summary>An uploaded file is judged by its own first bytes and its size, never by the name or the type the sender wrote.</summary>
public sealed class UploadHardeningHttpTests
{
    private static ByteArrayContent Bytes(byte[] bytes) => new(bytes);

    [Theory]
    [InlineData("pdf", "%PDF-1.7 certificate", true)]
    [InlineData("png", null, true)]                      // a real PNG
    [InlineData("jpg", null, true)]
    [InlineData("pdf", "<script>alert(1)</script>", false)]   // a script called .pdf
    [InlineData("jpg", "MZ this is an executable", false)]
    [InlineData("pdf", "plain text", false)]
    [InlineData("webp", null, false)]                    // a photo format, but not one of the document formats
    public async Task A_business_document_must_be_a_pdf_jpeg_or_png_by_its_bytesAsync(string extension, string? text, bool accepted)
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop" });
        var bytes = text is not null ? Encoding.UTF8.GetBytes(text)
            : ImageVariantMakerTests.Image(extension switch { "png" => SKEncodedImageFormat.Png, "jpg" => SKEncodedImageFormat.Jpeg, _ => SKEncodedImageFormat.Webp }, 50, 40, SKColors.Gray);

        var response = await owner.PostAsync("/api/v1/businesses/me/documents?docType=Gst", new MultipartFormDataContent { { Bytes(bytes), "file", "gst." + extension } });

        if (accepted) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("document_invalid_type", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task A_business_document_over_10_MB_is_refusedAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "owner");
        await owner.PostAsJsonAsync("/api/v1/businesses", new { businessType = "Retailer", legalName = "Shop", displayName = "Shop" });
        var big = new byte[UploadRules.MaxDocumentBytes + 1];
        Encoding.ASCII.GetBytes("%PDF-1.4").CopyTo(big, 0);
        var response = await owner.PostAsync("/api/v1/businesses/me/documents?docType=Gst", new MultipartFormDataContent { { Bytes(big), "file", "big.pdf" } });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("document_too_large", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static async Task<(MarketplaceHttpFixture Fixture, HttpClient Seller)> BulkAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapBulkProductEndpoints());
        await CatalogueHttpTests.SeedAsync(fixture); // variants PUFFER-L and FLEECE-M of seller 10
        return (fixture, await fixture.ClientAsync("Manufacturer", "seller-user", 10));
    }

    [Fact]
    public async Task Bulk_photos_keep_only_real_photos_of_a_sensible_size_and_name_the_ones_they_refuseAsync()
    {
        var (fixture, seller) = await BulkAsync();
        await using var _ = fixture; using var __ = seller;
        var real = ImageVariantMakerTests.Image(SKEncodedImageFormat.Png, 60, 40, SKColors.Red);
        var tooBig = new byte[UploadRules.MaxImageBytes + 1];
        real.CopyTo(tooBig, 0);
        var form = new MultipartFormDataContent
        {
            { Bytes(real), "files", "PUFFER-L.png" },
            { Bytes(Encoding.UTF8.GetBytes("<script>alert(1)</script>")), "files", "FLEECE-M.jpg" },   // not a photo, whatever it is called
            { Bytes(real), "files", "NO-SUCH-SKU.png" },
            { Bytes(tooBig), "files", "PUFFER-L.jpg" }
        };

        var result = await (await seller.PostAsync("/api/v1/seller/products/bulk/images", form)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, result.GetProperty("uploaded").GetInt32());
        Assert.Equal(new[] { "NO-SUCH-SKU" }, result.GetProperty("missing").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "FLEECE-M.jpg", "PUFFER-L.jpg" }, result.GetProperty("rejected").EnumerateArray().Select(x => x.GetString()).OrderBy(x => x));
    }

    [Fact]
    public async Task Bulk_photos_are_limited_to_100_filesAsync()
    {
        var (fixture, seller) = await BulkAsync();
        await using var _ = fixture; using var __ = seller;
        var tiny = ImageVariantMakerTests.Image(SKEncodedImageFormat.Png, 8, 8, SKColors.Red);
        var form = new MultipartFormDataContent();
        for (var i = 0; i < 101; i++) form.Add(Bytes(tiny), "files", $"SKU-{i}.png");
        var response = await seller.PostAsync("/api/v1/seller/products/bulk/images", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("too_many_files", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
