using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Catalogue;

public sealed class EfProductDirectory(MarketplaceDbContext db) : IProductDirectory
{
    public Product Add(long businessId, Product product) { product.SellerBusinessId = businessId; db.Products.Add(product); db.SaveChanges(); return product; }
    public IReadOnlyList<Product> ListForSeller(long businessId) => db.Products.Where(x => x.SellerBusinessId == businessId && !x.IsDeleted).ToList();
    public bool Remove(long businessId, long productId) { var product = db.Products.SingleOrDefault(x => x.Id == productId && x.SellerBusinessId == businessId && !x.IsDeleted); if (product is null) return false; product.IsDeleted = true; db.SaveChanges(); return true; }
}
