using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TextileConnect.Api;

namespace TextileConnect.Tests;

public sealed class SellerProductMediaHttpTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2, 3, 4];
    private static readonly byte[] Webp = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2];

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
        var owned = await seller.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        Assert.Equal("image/png", owned.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await owned.Content.ReadAsByteArrayAsync());
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
}
