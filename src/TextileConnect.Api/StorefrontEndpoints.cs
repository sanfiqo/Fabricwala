using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class StorefrontEndpoints
{
    public static void MapStorefrontEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/storefronts/{businessId:long}", async (long businessId, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var business = await db.Businesses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == businessId && x.BusinessType == BusinessType.Manufacturer && x.VerificationStatus == VerificationStatus.Approved && !x.IsDeleted, ct);
            if (business is null) return Results.NotFound();
            var city = await db.BusinessAddresses.Where(x => x.BusinessId == businessId && x.AddressType == AddressType.Pickup).OrderByDescending(x => x.IsDefault).Join(db.Cities, x => x.CityId, x => x.Id, (_, value) => value.Name).FirstOrDefaultAsync(ct);
            var products = await db.Products.AsNoTracking().Where(x => x.SellerBusinessId == businessId && x.Status == ProductStatus.Active && !x.IsDeleted).OrderByDescending(x => x.Id).ToListAsync(ct);
            var productIds = products.Select(x => x.Id).ToArray();
            var prices = await db.PriceSlabs.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).GroupBy(x => x.ProductId).Select(x => new { ProductId = x.Key, From = x.Min(y => y.PricePerUnit), To = x.Max(y => y.PricePerUnit) }).ToDictionaryAsync(x => x.ProductId, ct);
            var ratings = await db.Reviews.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).GroupBy(x => 1).Select(x => new { Average = x.Average(y => y.Rating), Count = x.Count() }).SingleOrDefaultAsync(ct);
            return Results.Ok(new { business = new { business.Id, business.DisplayName, business.Email, city }, rating = ratings?.Average ?? 0, reviewCount = ratings?.Count ?? 0, products = products.Select(product => new ProductSummaryResponse(product.Id, product.Slug, product.Title, product.CategoryId, business.Id, business.DisplayName, city ?? string.Empty, null, product.Moq, product.Unit, prices.GetValueOrDefault(product.Id)?.From ?? 0, prices.GetValueOrDefault(product.Id)?.To ?? 0, null, product.Status)).ToList() });
        });
    }
}
