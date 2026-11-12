using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Application.Catalogue;

public interface IProductDirectory
{
    Product Add(long businessId, Product product);
    IReadOnlyList<Product> ListForSeller(long businessId);
    bool Remove(long businessId, long productId);
}
