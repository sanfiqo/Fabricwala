using System.Text.Json;
using TextileConnect.Api;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Tests;

public sealed class ApiResponseDtoTests
{
    [Fact]
    public void Product_summary_uses_the_pwa_shape()
    {
        var json = JsonSerializer.Serialize(ProductSummaryResponse.From(new Product
        {
            Id = 7, SellerBusinessId = 9, CategoryId = 2, Title = "Coat", Slug = "coat", Moq = 10,
            Unit = ProductUnit.Piece, Status = ProductStatus.Active
        }), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"sellerBusinessId\":9", json);
        Assert.Contains("\"priceFrom\":0", json);
        Assert.DoesNotContain("productId", json);
    }

    [Fact]
    public void Product_detail_is_flattened_and_has_contract_collections()
    {
        var detail = new ProductDetail(
            new Product { Id = 1, SellerBusinessId = 2, CategoryId = 3, Title = "Coat", Slug = "coat", Moq = 1 },
            [new ProductVariant { Id = 4, ProductId = 1, Sku = "C-1" }],
            [new PriceSlab { Id = 5, ProductId = 1, MinQty = 1, PricePerUnit = 100 }], []);

        var result = ProductDetailResponse.From(detail);

        Assert.Equal(4, result.Variants[0].Id);
        Assert.Equal(5, result.Slabs[0].Id);
        Assert.Null(result.Material);
    }
}
