using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class ProductSearchTests
{
    [Fact]
    public void Public_search_filters_description_and_moq_and_sorts_by_price()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new MarketplaceDbContext(options);
        var first = new Product { Id = 1, SellerBusinessId = 10, CategoryId = 2, Title = "Cotton Roll", Slug = "cotton-roll", Description = "premium blue fabric", Moq = 10, Status = ProductStatus.Active };
        var second = new Product { Id = 2, SellerBusinessId = 10, CategoryId = 2, Title = "Silk Roll", Slug = "silk-roll", Description = "premium fabric", Moq = 20, Status = ProductStatus.Active };
        db.Products.AddRange(first, second);
        db.PriceSlabs.AddRange(new PriceSlab { ProductId = 1, MinQty = 10, PricePerUnit = 90 }, new PriceSlab { ProductId = 2, MinQty = 20, PricePerUnit = 70 });
        db.SaveChanges();

        var result = new EfProductDirectory(db).ListPublic("premium", null, null, null, 10, "price-asc", 1, 20, out var total);

        Assert.Equal(1, total);
        Assert.Single(result);
        Assert.Equal("cotton-roll", result[0].Slug);
    }
}
