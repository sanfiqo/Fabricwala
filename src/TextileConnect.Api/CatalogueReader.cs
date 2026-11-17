using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed class CatalogueReader(MarketplaceDbContext db)
{
    public IQueryable<Product> PublicProducts => db.Products.AsNoTracking().Where(p => !p.IsDeleted && p.Status == ProductStatus.Active
        && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved));

    public async Task<PagedResponse<ProductSummaryResponse>> ListAsync(CatalogueQuery query, CancellationToken ct)
    {
        var rows = from product in PublicProducts
                   join seller in db.Businesses.AsNoTracking() on product.SellerBusinessId equals seller.Id
                   where db.PriceSlabs.Any(x => x.ProductId == product.Id)
                   select new CatalogueRow
                   {
                       Product = product, SellerName = seller.DisplayName,
                       CityId = db.BusinessAddresses.Where(a => a.BusinessId == seller.Id && (a.AddressType == AddressType.Pickup || a.AddressType == AddressType.Registered))
                           .OrderBy(a => a.AddressType == AddressType.Pickup ? 0 : 1).ThenByDescending(a => a.IsDefault).ThenBy(a => a.Id).Select(a => (long?)a.CityId).FirstOrDefault(),
                       PriceFrom = db.PriceSlabs.Where(x => x.ProductId == product.Id).Min(x => x.PricePerUnit),
                       PriceTo = db.PriceSlabs.Where(x => x.ProductId == product.Id).Max(x => x.PricePerUnit),
                       UnitsSold = product.UnitsSold
                   };
        var text = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(text)) rows = rows.Where(x => x.Product.Title.ToLower().Contains(text) || x.Product.Description != null && x.Product.Description.ToLower().Contains(text) || x.Product.Material != null && x.Product.Material.ToLower().Contains(text));
        if (query.CategoryId.HasValue) rows = rows.Where(x => x.Product.CategoryId == query.CategoryId);
        if (query.MinPrice.HasValue) rows = rows.Where(x => x.PriceFrom >= query.MinPrice);
        if (query.MaxPrice.HasValue) rows = rows.Where(x => x.PriceFrom <= query.MaxPrice);
        if (query.MaxMoq.HasValue) rows = rows.Where(x => x.Product.Moq <= query.MaxMoq);
        if (query.StateId.HasValue) rows = rows.Where(x => db.Cities.Any(c => c.Id == x.CityId && c.StateId == query.StateId));
        if (!string.IsNullOrWhiteSpace(query.State)) rows = rows.Where(x => db.Cities.Any(c => c.Id == x.CityId && db.States.Any(s => s.Id == c.StateId && (s.Code == query.State || s.Name == query.State))));
        if (query.SellerCityId.HasValue) rows = rows.Where(x => x.CityId == query.SellerCityId);
        if (!string.IsNullOrWhiteSpace(query.Material)) rows = rows.Where(x => x.Product.Material != null && x.Product.Material.Contains(query.Material));
        if (!string.IsNullOrWhiteSpace(query.Season)) rows = rows.Where(x => x.Product.Season != null && x.Product.Season.Contains(query.Season));
        if (!string.IsNullOrWhiteSpace(query.Size)) rows = rows.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Product.Id && v.IsActive && v.Size == query.Size));
        if (!string.IsNullOrWhiteSpace(query.Colour)) rows = rows.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Product.Id && v.IsActive && v.Colour == query.Colour));
        var total = await rows.CountAsync(ct);
        var sorted = query.Sort?.ToLowerInvariant() switch
        {
            "popular" => rows.OrderByDescending(x => x.UnitsSold).ThenByDescending(x => x.Product.Id),
            "price-asc" => rows.OrderBy(x => x.PriceFrom).ThenBy(x => x.Product.Id),
            "price-desc" => rows.OrderByDescending(x => x.PriceFrom).ThenBy(x => x.Product.Id),
            "moq-asc" => rows.OrderBy(x => x.Product.Moq).ThenBy(x => x.Product.Id),
            _ => rows.OrderByDescending(x => x.Product.Id)
        };
        var page = await sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var ids = page.Select(x => x.Product.Id).ToArray();
        var cityIds = page.Where(x => x.CityId.HasValue).Select(x => x.CityId!.Value).Distinct().ToArray();
        var cities = await db.Cities.AsNoTracking().Where(x => cityIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var media = await db.ProductMedia.AsNoTracking().Where(x => ids.Contains(x.ProductId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var stock = await db.ProductVariants.AsNoTracking().Where(x => ids.Contains(x.ProductId) && x.IsActive).GroupBy(x => x.ProductId).Select(x => new { Id = x.Key, Quantity = x.Sum(v => v.StockQty) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        return new(page.Select(x => new ProductSummaryResponse(x.Product.Id, x.Product.Slug, x.Product.Title, x.Product.CategoryId,
            x.Product.SellerBusinessId, x.SellerName, x.CityId.HasValue ? cities.GetValueOrDefault(x.CityId.Value) ?? string.Empty : string.Empty,
            media.FirstOrDefault(m => m.ProductId == x.Product.Id) is { } image ? MediaUrl(x.Product.Slug, image) : null,
            x.Product.Moq, x.Product.Unit, x.PriceFrom, x.PriceTo, x.Product.Material, x.Product.Status)
            { AvailableStock = stock.GetValueOrDefault(x.Product.Id), UnitsSold = x.UnitsSold }).ToList(), total, query.Page, query.PageSize);
    }

    public async Task<ProductDetailResponse?> DetailAsync(string slug, CancellationToken ct)
    {
        var product = await PublicProducts.SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (product is null) return null;
        var seller = await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == product.SellerBusinessId, ct);
        var city = await db.BusinessAddresses.Where(a => a.BusinessId == seller.Id && (a.AddressType == AddressType.Pickup || a.AddressType == AddressType.Registered))
            .OrderBy(a => a.AddressType == AddressType.Pickup ? 0 : 1).ThenByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Join(db.Cities, a => a.CityId, c => c.Id, (_, c) => c.Name).FirstOrDefaultAsync(ct);
        var variants = await db.ProductVariants.AsNoTracking().Where(x => x.ProductId == product.Id && x.IsActive).OrderBy(x => x.Id).ToListAsync(ct);
        var slabs = await db.PriceSlabs.AsNoTracking().Where(x => x.ProductId == product.Id).OrderBy(x => x.MinQty).ThenBy(x => x.Id).ToListAsync(ct);
        var media = await db.ProductMedia.AsNoTracking().Where(x => x.ProductId == product.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var result = ProductDetailResponse.From(new(product, variants, slabs, media));
        return result with { SellerName = seller.DisplayName, SellerCity = city ?? string.Empty,
            ImageUrl = media.FirstOrDefault() is { } image ? MediaUrl(slug, image) : null,
            Media = media.Select(x => new ProductMediaResponse(x.Id, MediaUrl(slug, x), x.MediaType, x.SortOrder)).ToList() };
    }

    public static string MediaUrl(string slug, ProductMedia media) => Uri.TryCreate(media.FileKey, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
        ? uri.AbsoluteUri : $"/api/v1/products/{Uri.EscapeDataString(slug)}/media/{media.Id}";

    private sealed class CatalogueRow
    {
        public required Product Product { get; init; }
        public required string SellerName { get; init; }
        public long? CityId { get; init; }
        public decimal PriceFrom { get; init; }
        public decimal PriceTo { get; init; }
        public int UnitsSold { get; init; }
    }
}
