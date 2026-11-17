using TextileConnect.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class CatalogueHttpTests
{
    [Fact]
    public async Task Home_categories_search_and_detail_return_real_catalogue_fieldsAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapCatalogueEndpoints());
        await SeedAsync(fixture);
        using var client = await fixture.ClientAsync(null);
        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        Assert.NotEmpty(home.GetProperty("categories").EnumerateArray());
        Assert.Equal("fleece-hoodie", home.GetProperty("bestSellers").GetProperty("items")[0].GetProperty("slug").GetString());
        var categories = await client.GetFromJsonAsync<JsonElement>("/api/v1/categories?pageSize=2");
        Assert.Equal(2, categories.GetProperty("items").GetArrayLength());
        Assert.True(categories.GetProperty("items")[0].GetProperty("id").GetInt64() > 0);
        var result = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?q=POLYESTER&state=PB&minPrice=430&maxPrice=450&maxMoq=50&size=L&colour=Black");
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        var card = result.GetProperty("items")[0];
        Assert.Equal("Kapoor Knits", card.GetProperty("sellerName").GetString());
        Assert.Equal("Ludhiana", card.GetProperty("sellerCity").GetString());
        Assert.Equal(440m, card.GetProperty("priceFrom").GetDecimal());
        Assert.Equal(480m, card.GetProperty("priceTo").GetDecimal());
        Assert.Equal(1000, card.GetProperty("availableStock").GetInt32());
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/v1/products/puffer");
        Assert.Equal(3, detail.GetProperty("slabs").GetArrayLength());
        Assert.Single(detail.GetProperty("variants").EnumerateArray());
        Assert.Equal("Polyester", detail.GetProperty("material").GetString());
        Assert.Equal(800m, detail.GetProperty("weightPerUnitGrams").GetDecimal());
        Assert.Equal("quilted", detail.GetProperty("fabricSpec").GetString());
        Assert.Contains("/api/v1/products/puffer/media/", detail.GetProperty("media")[0].GetProperty("url").GetString());
        Assert.DoesNotContain("private-puffer.png", detail.GetRawText());
    }

    [Fact]
    public async Task Search_filters_by_material_and_seasonAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapCatalogueEndpoints());
        await SeedAsync(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            (await db.Products.SingleAsync(x => x.Slug == "puffer")).Season = "Winter";
            (await db.Products.SingleAsync(x => x.Slug == "fleece-hoodie")).Season = "Autumn";
            await db.SaveChangesAsync();
        }
        using var client = await fixture.ClientAsync(null);
        async Task<string[]> SlugsAsync(string query)
            => (await client.GetFromJsonAsync<JsonElement>("/api/v1/products?" + query)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("slug").GetString()!).ToArray();

        Assert.Equal(new[] { "puffer" }, await SlugsAsync("season=Winter"));
        Assert.Equal(new[] { "fleece-hoodie" }, await SlugsAsync("season=Autumn"));
        Assert.Empty(await SlugsAsync("season=Summer"));
        Assert.Equal(new[] { "puffer" }, await SlugsAsync("material=Polyester&season=Winter"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/products?season=" + new string('x', 81))).StatusCode);
    }

    [Fact]
    public async Task Catalogue_paging_is_stable_and_hidden_products_are_not_publicAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapCatalogueEndpoints());
        await SeedAsync(fixture);
        using var client = await fixture.ClientAsync(null);
        var first = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?sort=price-desc&pageSize=1&page=1");
        var second = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?sort=price-desc&pageSize=1&page=2");
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        Assert.Equal("puffer", first.GetProperty("items")[0].GetProperty("slug").GetString());
        Assert.Equal("fleece-hoodie", second.GetProperty("items")[0].GetProperty("slug").GetString());
        foreach (var slug in new[] { "draft", "unapproved", "missing" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/products/" + slug)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/products/draft/media/1")).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=101", "minPrice=500&maxPrice=100", "sort=unknown", "maxMoq=0", "pincode=bad" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/products?" + query)).StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/v1/products?state=MP");
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
    }

    internal static async Task SeedAsync(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var city = await db.Cities.SingleAsync(x => x.Name == "Ludhiana");
        var category = await db.Categories.SingleAsync(x => x.Slug == "winter-wear");
        db.Businesses.AddRange(
            new Business { Id = 10, BusinessType = BusinessType.Manufacturer, LegalName = "Kapoor Knits", DisplayName = "Kapoor Knits", Phone = "+919000000002", VerificationStatus = VerificationStatus.Approved },
            new Business { Id = 20, BusinessType = BusinessType.Manufacturer, LegalName = "Pending", DisplayName = "Pending", Phone = "+919000000004" },
            new Business { Id = 30, BusinessType = BusinessType.Retailer, LegalName = "Retailer", DisplayName = "Retailer", Phone = "+919000000001", VerificationStatus = VerificationStatus.Approved });
        db.BusinessAddresses.Add(new BusinessAddress { BusinessId = 10, AddressType = AddressType.Pickup, Line1 = "Test pickup", CityId = city.Id, Pincode = "141001", IsDefault = true });
        db.Products.AddRange(
            new Product { Id = 1, SellerBusinessId = 10, CategoryId = category.Id, Title = "Quilted puffer jacket", Slug = "puffer", Material = "Polyester", FabricSpec = "quilted", WeightPerUnitGrams = 800, Moq = 50, Status = ProductStatus.Active, GstRate = 5 },
            new Product { Id = 2, SellerBusinessId = 10, CategoryId = category.Id, Title = "Fleece hoodie", Slug = "fleece-hoodie", Material = "Fleece", Moq = 20, Status = ProductStatus.Active, GstRate = 5 },
            new Product { Id = 3, SellerBusinessId = 10, CategoryId = category.Id, Title = "Draft", Slug = "draft", Moq = 20, Status = ProductStatus.Draft },
            new Product { Id = 4, SellerBusinessId = 20, CategoryId = category.Id, Title = "Unapproved", Slug = "unapproved", Moq = 20, Status = ProductStatus.Active });
        db.ProductVariants.AddRange(new ProductVariant { Id = 1, ProductId = 1, Sku = "PUFFER-L", Size = "L", Colour = "Black", StockQty = 1000 },
            new ProductVariant { Id = 2, ProductId = 1, Sku = "PUFFER-XS", Size = "XS", Colour = "Black", StockQty = 10, IsActive = false },
            new ProductVariant { Id = 3, ProductId = 2, Sku = "FLEECE-M", Size = "M", Colour = "Red", StockQty = 1000 });
        db.PriceSlabs.AddRange(new PriceSlab { ProductId = 1, MinQty = 50, MaxQty = 199, PricePerUnit = 480 },
            new PriceSlab { ProductId = 1, MinQty = 200, MaxQty = 499, PricePerUnit = 459 },
            new PriceSlab { ProductId = 1, MinQty = 500, PricePerUnit = 440 },
            new PriceSlab { ProductId = 2, MinQty = 20, PricePerUnit = 300 });
        db.ProductMedia.Add(new ProductMedia { ProductId = 1, FileKey = "private-puffer.png", MediaType = "Image" });
        var order = new Order { OrderNumber = "PAID-POPULARITY", RetailerBusinessId = 30, PaymentStatus = PaymentStatus.Paid };
        var sellerOrder = new SellerOrder { Order = order, ManufacturerBusinessId = 10, Status = SellerOrderStatus.Accepted };
        db.OrderItems.Add(new OrderItem { SellerOrder = sellerOrder, ProductId = 2, Quantity = 100, UnitPrice = 300 });
        await db.SaveChangesAsync();
    }
}
