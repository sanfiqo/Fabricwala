using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Catalogue;

public sealed class EfProductDirectory(MarketplaceDbContext db) : IProductDirectory
{
    public Product Add(long businessId, Product product) { product.SellerBusinessId = businessId; db.Products.Add(product); db.SaveChanges(); return product; }
    public ProductDetail AddAggregate(long businessId, Product product, IReadOnlyList<ProductVariant> variants, IReadOnlyList<PriceSlab> slabs)
    {
        product.SellerBusinessId = businessId;
        var errors = PriceSlabRules.Validate(product, slabs);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors));
        db.Products.Add(product); db.SaveChanges();
        foreach (var variant in variants) { variant.ProductId = product.Id; db.ProductVariants.Add(variant); }
        foreach (var slab in slabs) { slab.ProductId = product.Id; db.PriceSlabs.Add(slab); }
        db.SaveChanges();
        return new ProductDetail(product, variants, slabs, Array.Empty<ProductMedia>());
    }
    public IReadOnlyList<Product> ListForSeller(long businessId) => db.Products.Where(x => x.SellerBusinessId == businessId && !x.IsDeleted).ToList();
    public Product? GetForSeller(long businessId, long productId) => db.Products.SingleOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId && !x.IsDeleted);
    public Product? GetBySlug(string slug) => db.Products.SingleOrDefault(x => x.Slug == slug && x.Status == ProductStatus.Active && !x.IsDeleted);
    public bool Remove(long businessId, long productId) { var product = db.Products.SingleOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId && !x.IsDeleted); if (product is null) return false; product.IsDeleted = true; db.SaveChanges(); return true; }
    public IReadOnlyList<Product> ListPublic(string? query, long? categoryId, decimal? minPrice, decimal? maxPrice, int? maxMoq, string? sort, int page, int pageSize, out int total)
    {
        var products = db.Products.Where(x => x.Status == ProductStatus.Active && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query)) products = products.Where(x => x.Title.Contains(query) || (x.Description != null && x.Description.Contains(query)));
        if (categoryId is not null) products = products.Where(x => x.CategoryId == categoryId);
        if (minPrice is not null) products = products.Where(x => db.PriceSlabs.Any(s => s.ProductId == x.Id && s.PricePerUnit >= minPrice));
        if (maxPrice is not null) products = products.Where(x => db.PriceSlabs.Any(s => s.ProductId == x.Id && s.PricePerUnit <= maxPrice));
        if (maxMoq is not null) products = products.Where(x => x.Moq <= maxMoq);
        total = products.Count();
        products = sort?.ToLowerInvariant() switch
        {
            "price-asc" => products.OrderBy(x => db.PriceSlabs.Where(s => s.ProductId == x.Id).Select(s => (decimal?)s.PricePerUnit).Min() ?? decimal.MaxValue),
            "price-desc" => products.OrderByDescending(x => db.PriceSlabs.Where(s => s.ProductId == x.Id).Select(s => (decimal?)s.PricePerUnit).Min() ?? decimal.MinValue),
            "moq-asc" => products.OrderBy(x => x.Moq),
            "new" or "newest" => products.OrderByDescending(x => x.Id),
            _ => products.OrderBy(x => x.Id)
        };
        return products.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToList();
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
