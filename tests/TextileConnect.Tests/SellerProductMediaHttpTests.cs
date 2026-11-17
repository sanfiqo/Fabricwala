using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class SellerProductMediaHttpTests
{
    // Real photos: the upload now reads them to make the small copies.
    private static readonly byte[] Png = ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Png, 600, 400, SkiaSharp.SKColors.Red);
    private static readonly byte[] Jpeg = ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Jpeg, 500, 500, SkiaSharp.SKColors.Blue);
    private static readonly byte[] Webp = ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Webp, 400, 300, SkiaSharp.SKColors.Green);

    private static async Task<MarketplaceHttpFixture> StartAsync()
    {
        var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        await CatalogueHttpTests.SeedAsync(fixture); // seller 10 owns products 1 (active), 2 (active), 3 (draft)
        return fixture;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, long productId, byte[] bytes, string fileName = "photo.bin")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return client.PostAsync($"/api/v1/seller/products/{productId}/media", new MultipartFormDataContent { { file, "file", fileName } });
    }

    [Fact]
    public async Task Photos_are_added_in_order_shown_in_the_detail_and_visible_to_the_owner_before_publishingAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };

        var first = await UploadAsync(seller, 3, Png, "front.exe"); // the name is ignored, the bytes decide
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var main = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("image/png", main.GetProperty("mediaType").GetString());
        Assert.True(main.GetProperty("isMain").GetBoolean());
        Assert.Equal(0, main.GetProperty("sortOrder").GetInt32());

        var second = await (await UploadAsync(seller, 3, Jpeg)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(second.GetProperty("isMain").GetBoolean());
        Assert.Equal(1, second.GetProperty("sortOrder").GetInt32());
        Assert.Equal("image/webp", (await (await UploadAsync(seller, 3, Webp)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mediaType").GetString());

        var detail = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/products/3");
        var media = detail.GetProperty("media").EnumerateArray().ToList();
        Assert.Equal(3, media.Count);
        Assert.True(media[0].GetProperty("isMain").GetBoolean());
        Assert.Equal(main.GetProperty("id").GetInt64(), media[0].GetProperty("id").GetInt64());

        // The product is still a draft: the owner can open the photo, other people cannot.
        var url = main.GetProperty("url").GetString()!;
        var owned = await seller.GetAsync(url);                                   // the address in the response is the 800 pixel copy
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        Assert.Equal("image/webp", owned.Content.Headers.ContentType!.MediaType);
        var original = await seller.GetAsync(url.Split('?')[0]);                  // without ?size the original comes back unchanged
        Assert.Equal("image/png", original.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await original.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url)).StatusCode);
        using var otherSeller = await fixture.ClientAsync("Manufacturer", "other-user", 11);
        Assert.Equal(HttpStatusCode.NotFound, (await otherSeller.GetAsync(url)).StatusCode);

        // A live product's photos are open to everyone.
        var live = await (await UploadAsync(seller, 1, Jpeg)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(live.GetProperty("url").GetString())).StatusCode);
        var list = await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/products");
        Assert.False(live.GetProperty("isMain").GetBoolean()); // the seeded product already had a photo
        Assert.NotNull(list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == 1).GetProperty("imageUrl").GetString());
    }

    [Fact]
    public async Task Bad_files_too_many_photos_other_sellers_and_roles_are_refusedAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);

        async Task<string> CodeAsync(HttpResponseMessage response, HttpStatusCode status)
        {
            Assert.Equal(status, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        }

        Assert.Equal("photo_invalid_type", await CodeAsync(await UploadAsync(seller, 3, Encoding.UTF8.GetBytes("not an image"), "photo.jpg"), HttpStatusCode.BadRequest));
        Assert.Equal("photo_invalid_type", await CodeAsync(await UploadAsync(seller, 3, Encoding.UTF8.GetBytes("<svg onload=alert(1)>"), "photo.svg"), HttpStatusCode.BadRequest));
        var big = new byte[SellerProductMediaEndpoints.MaxBytes + 1];
        Png.CopyTo(big, 0);
        Assert.Equal("photo_too_large", await CodeAsync(await UploadAsync(seller, 3, big), HttpStatusCode.RequestEntityTooLarge));

        for (var i = 0; i < SellerProductMediaEndpoints.MaxPhotosPerProduct; i++) Assert.Equal(HttpStatusCode.Created, (await UploadAsync(seller, 3, Png)).StatusCode);
        Assert.Equal("photo_limit_reached", await CodeAsync(await UploadAsync(seller, 3, Png), HttpStatusCode.Conflict));

        using var otherSeller = await fixture.ClientAsync("Manufacturer", "other-user", 11);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(otherSeller, 3, Png)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(seller, 999, Png)).StatusCode);
        using var retailer = await fixture.ClientAsync("Retailer", "retailer-user", 20);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(retailer, 3, Png)).StatusCode);
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(anonymous, 3, Png)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_photo_removes_it_and_the_next_one_becomes_the_main_imageAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var first = await (await UploadAsync(seller, 3, Png)).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await UploadAsync(seller, 3, Jpeg)).Content.ReadFromJsonAsync<JsonElement>();

        using var otherSeller = await fixture.ClientAsync("Manufacturer", "other-user", 11);
        Assert.Equal(HttpStatusCode.NotFound, (await otherSeller.DeleteAsync($"/api/v1/seller/products/3/media/{first.GetProperty("id").GetInt64()}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await seller.DeleteAsync($"/api/v1/seller/products/3/media/{first.GetProperty("id").GetInt64()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.DeleteAsync($"/api/v1/seller/products/3/media/{first.GetProperty("id").GetInt64()}")).StatusCode);
        var media = (await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/products/3")).GetProperty("media").EnumerateArray().ToList();
        var left = Assert.Single(media);
        Assert.Equal(second.GetProperty("id").GetInt64(), left.GetProperty("id").GetInt64());
        Assert.True(left.GetProperty("isMain").GetBoolean());
    }

    private static (int Width, int Height, string Format) Describe(byte[] bytes)
    {
        using var codec = SkiaSharp.SKCodec.Create(new MemoryStream(bytes));
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat.ToString());
    }

    [Fact]
    public async Task An_upload_comes_with_square_copies_a_src_set_and_a_blur_previewAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };

        var photo = await (await UploadAsync(seller, 2, Png)).Content.ReadFromJsonAsync<JsonElement>(); // product 2 is live and has no photo yet
        var main = photo.GetProperty("url").GetString()!;
        Assert.EndsWith("?size=main", main);
        var srcSet = photo.GetProperty("srcSet").GetString()!;
        Assert.Contains("?size=card 400w", srcSet);
        Assert.Contains("?size=main 800w", srcSet);
        Assert.StartsWith("data:image/jpeg;base64,", photo.GetProperty("lqip").GetString());

        foreach (var (size, pixels) in new[] { ("main", 800), ("card", 400), ("thumb", 120) })
        {
            var response = await anonymous.GetAsync(main.Replace("size=main", "size=" + size)); // product 2 is live, so anyone can open it
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/webp", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal((pixels, pixels, "Webp"), Describe(await response.Content.ReadAsByteArrayAsync()));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync(main.Split('?')[0] + "?size=huge")).StatusCode);

        // The shop pages use the copies: the card for lists, the 800 copy on the product page.
        var card = (await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/products")).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "fleece-hoodie");
        Assert.EndsWith("?size=card", card.GetProperty("imageUrl").GetString());
        Assert.Contains("400w", card.GetProperty("imageSrcSet").GetString());
        Assert.StartsWith("data:image/jpeg;base64,", card.GetProperty("imageLqip").GetString());
        var page = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/products/fleece-hoodie");
        Assert.EndsWith("?size=main", page.GetProperty("imageUrl").GetString());
        Assert.Equal(srcSet, page.GetProperty("media")[0].GetProperty("srcSet").GetString());
        Assert.Equal(photo.GetProperty("lqip").GetString(), page.GetProperty("imageLqip").GetString());

        // The seller's edit screen gets the same fields.
        var detail = (await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/products/2")).GetProperty("media")[0];
        Assert.Equal(srcSet, detail.GetProperty("srcSet").GetString());
        Assert.NotNull(detail.GetProperty("lqip").GetString());
    }

    [Fact]
    public async Task A_photo_without_copies_still_works_and_has_no_src_setAsync()
    {
        await using var fixture = await StartAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };
        // Product 1 has the photo that exists in the shared test data, added before copies were made.
        var page = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/products/puffer");
        var legacy = page.GetProperty("media")[0];
        Assert.Equal(JsonValueKind.Null, legacy.GetProperty("srcSet").ValueKind);
        Assert.Equal(JsonValueKind.Null, legacy.GetProperty("lqip").ValueKind);
        Assert.DoesNotContain("size=", legacy.GetProperty("url").GetString());

        // A photo that has no copy of the asked size gives the original instead: strip the copies of a fresh upload to look like an old photo.
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var uploaded = await (await UploadAsync(seller, 2, Png)).Content.ReadFromJsonAsync<JsonElement>();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TextileConnect.Infrastructure.Persistence.MarketplaceDbContext>();
            var row = await db.ProductMedia.SingleAsync(x => x.Id == uploaded.GetProperty("id").GetInt64());
            row.MainKey = row.CardKey = row.ThumbKey = row.Lqip = null;
            await db.SaveChangesAsync();
        }
        var path = uploaded.GetProperty("url").GetString()!.Split('?')[0];
        var fallback = await anonymous.GetAsync(path + "?size=card");
        Assert.Equal(HttpStatusCode.OK, fallback.StatusCode);
        Assert.Equal("image/png", fallback.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await fallback.Content.ReadAsByteArrayAsync());
        var plain = (await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/products/fleece-hoodie")).GetProperty("media")[0];
        Assert.Equal(path, plain.GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("srcSet").ValueKind);
    }

    [Fact]
    public async Task A_file_that_starts_like_a_photo_but_cannot_be_read_is_refusedAsync()
    {
        await using var fixture = await StartAsync();
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        var brokenPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6 }; // the signature of a PNG, then nothing
        var response = await UploadAsync(seller, 3, brokenPng);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("photo_unreadable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Empty((await seller.GetFromJsonAsync<JsonElement>("/api/v1/seller/products/3")).GetProperty("media").EnumerateArray()); // nothing was stored
    }
}
