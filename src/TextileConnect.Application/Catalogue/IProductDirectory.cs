using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Application.Catalogue;

public interface IProductDirectory
{
    Product Add(long businessId, Product product);
    ProductDetail AddAggregate(long businessId, Product product, IReadOnlyList<ProductVariant> variants, IReadOnlyList<PriceSlab> slabs);
    IReadOnlyList<Product> ListForSeller(long businessId);
    Product? GetForSeller(long businessId, long productId);
    Product? GetBySlug(string slug);
    bool Remove(long businessId, long productId);
    IReadOnlyList<Product> ListPublic(string? query, long? categoryId, decimal? minPrice, decimal? maxPrice, int? maxMoq, string? sort, int page, int pageSize, out int total);
    ProductDetail? GetDetail(string slug, bool sellerView = false);
}

public sealed record ProductDetail(Product Product, IReadOnlyList<ProductVariant> Variants, IReadOnlyList<PriceSlab> Slabs, IReadOnlyList<ProductMedia> Media);
