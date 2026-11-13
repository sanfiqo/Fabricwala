using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Catalogue;

public sealed class EfProductDirectory(MarketplaceDbContext db) : IProductDirectory
{
    public Product Add(long businessId, Product product) { product.SellerBusinessId = businessId; db.Products.Add(product); db.SaveChanges(); return product; }
    public IReadOnlyList<Product> ListForSeller(long businessId) => db.Products.Where(x => x.SellerBusinessId == businessId && !x.IsDeleted).ToList();
    public Product? GetForSeller(long businessId, long productId) => db.Products.SingleOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId && !x.IsDeleted);
    public Product? GetBySlug(string slug) => db.Products.SingleOrDefault(x => x.Slug == slug && x.Status == ProductStatus.Active && !x.IsDeleted);
    public bool Remove(long businessId, long productId) { var product = db.Products.SingleOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId && !x.IsDeleted); if (product is null) return false; product.IsDeleted = true; db.SaveChanges(); return true; }
    public IReadOnlyList<Product> ListPublic(string? query, long? categoryId, decimal? minPrice, decimal? maxPrice, int page, int pageSize, out int total)
    {
        var products = db.Products.Where(x => x.Status == ProductStatus.Active && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query)) products = products.Where(x => x.Title.Contains(query));
        if (categoryId is not null) products = products.Where(x => x.CategoryId == categoryId);
        if (minPrice is not null) products = products.Where(x => db.PriceSlabs.Any(s => s.ProductId == x.Id && s.PricePerUnit >= minPrice));
        if (maxPrice is not null) products = products.Where(x => db.PriceSlabs.Any(s => s.ProductId == x.Id && s.PricePerUnit <= maxPrice));
        total = products.Count();
        return products.OrderBy(x => x.Id).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToList();
    }
    public ProductDetail? GetDetail(string slug, bool sellerView = false)
    {
        var product = db.Products.SingleOrDefault(x => x.Slug == slug && !x.IsDeleted && (sellerView || x.Status == ProductStatus.Active));
        return product is null ? null : new ProductDetail(product,
            db.ProductVariants.Where(x => x.ProductId == product.Id && (sellerView || x.IsActive)).ToList(),
            db.PriceSlabs.Where(x => x.ProductId == product.Id).OrderBy(x => x.MinQty).ToList(),
            db.ProductMedia.Where(x => x.ProductId == product.Id).ToList());
    }
}
