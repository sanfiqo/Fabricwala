using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Application.Catalogue;

public interface IProductDirectory
{
    Product Add(string userId, Product product);
    IReadOnlyList<Product> ListForSeller(long businessId);
    bool Remove(long businessId, long productId);
}

public sealed class InMemoryProductDirectory : IProductDirectory
{
    private readonly List<Product> _products = [];
    public Product Add(string userId, Product product) { product.Id = _products.Count + 1; _products.Add(product); return product; }
    public IReadOnlyList<Product> ListForSeller(long businessId) => _products.Where(x => x.SellerBusinessId == businessId && !x.IsDeleted).ToList();
    public bool Remove(long businessId, long productId)
    {
        var product = _products.FirstOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId);
        if (product is null) return false;
        product.IsDeleted = true;
        return true;
    }
}
