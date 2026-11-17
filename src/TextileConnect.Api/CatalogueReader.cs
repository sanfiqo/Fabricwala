using TextileConnect.Application.Catalogue;
using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed class CatalogueReader(MarketplaceDbContext db)
{
    public IQueryable<Product> PublicProducts => PublicProductsOf(db.Products);

    private IQueryable<Product> PublicProductsOf(IQueryable<Product> source) => source.AsNoTracking().Where(p => !p.IsDeleted && p.Status == ProductStatus.Active
        && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved));

    private bool IsMySql => db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The filters a shopper can set. A facet count is worked out with every filter except its own, so the shopper sees what else they could pick.</summary>
    private enum Facet { None, Category, Material, Season, Size, Colour, State, City, Price }

    /// <summary>The products that match the query, with the fields the list sorts and filters on. Facet says which filter to leave out.</summary>
    private IQueryable<CatalogueRow> Rows(CatalogueQuery query, Facet skip)
    {
        var search = ProductTextSearch.For(query.Q);
        var useFullText = search.BooleanQuery is not null && IsMySql;
        // On MySQL a plain word search goes through the full-text index (FTX_Products_Search, migration AddProductFullText). The SELECT * is the same table, so every other filter still applies on top of it.
        var source = useFullText
            ? db.Products.FromSqlInterpolated($"SELECT * FROM `Products` WHERE MATCH(`Title`, `Description`, `Material`) AGAINST ({search.BooleanQuery} IN BOOLEAN MODE)")
            : db.Products;
        var rows = from product in PublicProductsOf(source)
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
        var text = search.Like;
        if (!string.IsNullOrEmpty(text) && !useFullText)
            rows = rows.Where(x => x.Product.Title.ToLower().Contains(text) || x.Product.Description != null && x.Product.Description.ToLower().Contains(text) || x.Product.Material != null && x.Product.Material.ToLower().Contains(text));
        if (query.CategoryId.HasValue && skip != Facet.Category) rows = rows.Where(x => x.Product.CategoryId == query.CategoryId);
        if (query.MinPrice.HasValue && skip != Facet.Price) rows = rows.Where(x => x.PriceFrom >= query.MinPrice);
        if (query.MaxPrice.HasValue && skip != Facet.Price) rows = rows.Where(x => x.PriceFrom <= query.MaxPrice);
        if (query.MaxMoq.HasValue) rows = rows.Where(x => x.Product.Moq <= query.MaxMoq);
        if (query.StateId.HasValue && skip != Facet.State) rows = rows.Where(x => db.Cities.Any(c => c.Id == x.CityId && c.StateId == query.StateId));
        if (!string.IsNullOrWhiteSpace(query.State) && skip != Facet.State) rows = rows.Where(x => db.Cities.Any(c => c.Id == x.CityId && db.States.Any(s => s.Id == c.StateId && (s.Code == query.State || s.Name == query.State))));
        if (query.SellerCityId.HasValue && skip != Facet.City) rows = rows.Where(x => x.CityId == query.SellerCityId);
        if (!string.IsNullOrWhiteSpace(query.Material) && skip != Facet.Material) rows = rows.Where(x => x.Product.Material != null && x.Product.Material.Contains(query.Material));
        if (!string.IsNullOrWhiteSpace(query.Season) && skip != Facet.Season) rows = rows.Where(x => x.Product.Season != null && x.Product.Season.Contains(query.Season));
        if (!string.IsNullOrWhiteSpace(query.Size) && skip != Facet.Size) rows = rows.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Product.Id && v.IsActive && v.Size == query.Size));
        if (!string.IsNullOrWhiteSpace(query.Colour) && skip != Facet.Colour) rows = rows.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Product.Id && v.IsActive && v.Colour == query.Colour));
        return rows;
    }

    private const int MaxFacetValues = 50;

    /// <summary>How many products each filter value would give, for the filter sheet. Takes the same parameters as the list (paging and sort are ignored).</summary>
    public async Task<ProductFiltersResponse> FacetsAsync(CatalogueQuery query, CancellationToken ct)
    {
        var total = await Rows(query, Facet.None).CountAsync(ct);

        var categoryCounts = await Rows(query, Facet.Category).GroupBy(x => x.Product.CategoryId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var categoryIds = categoryCounts.Select(x => x.Id).ToArray();
        var categoryNames = await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var categories = categoryCounts.Select(x => new FacetId(x.Id, categoryNames.GetValueOrDefault(x.Id) ?? string.Empty, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        async Task<List<FacetValue>> TextValuesAsync(Facet facet, System.Linq.Expressions.Expression<Func<CatalogueRow, string?>> column)
            => (await Rows(query, facet).Select(column).Where(x => x != null).GroupBy(x => x!).Select(g => new { Value = g.Key, Count = g.Count() }).ToListAsync(ct))
                .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        // Sizes and colours live on the variants. A product counts once for a size even when it has several colours of it.
        var sizeProducts = Rows(query, Facet.Size).Select(x => x.Product.Id);
        var sizes = (await db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Size != null && sizeProducts.Contains(v.ProductId))
                .GroupBy(v => v.Size!).Select(g => new { Value = g.Key, Count = g.Select(v => v.ProductId).Distinct().Count() }).ToListAsync(ct))
            .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();
        var colourProducts = Rows(query, Facet.Colour).Select(x => x.Product.Id);
        var colours = (await db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Colour != null && colourProducts.Contains(v.ProductId))
                .GroupBy(v => v.Colour!).Select(g => new { Value = g.Key, Count = g.Select(v => v.ProductId).Distinct().Count() }).ToListAsync(ct))
            .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        var cityCountsForState = await Rows(query, Facet.State).GroupBy(x => x.CityId).Select(g => new { CityId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var cityCounts = await Rows(query, Facet.City).GroupBy(x => x.CityId).Select(g => new { CityId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var allCityIds = cityCountsForState.Concat(cityCounts).Where(x => x.CityId.HasValue).Select(x => x.CityId!.Value).Distinct().ToArray();
        var cities = await db.Cities.AsNoTracking().Where(x => allCityIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var stateIds = cities.Values.Select(x => x.StateId).Distinct().ToArray();
        var states = await db.States.AsNoTracking().Where(x => stateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var stateOptions = cityCountsForState.Where(x => x.CityId.HasValue && cities.ContainsKey(x.CityId.Value))
            .GroupBy(x => cities[x.CityId!.Value].StateId).Select(g => new FacetState(g.Key, states[g.Key].Name, states[g.Key].Code, g.Sum(x => x.Count)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();
        var cityOptions = cityCounts.Where(x => x.CityId.HasValue && cities.ContainsKey(x.CityId.Value))
            .Select(x => new FacetId(x.CityId!.Value, cities[x.CityId.Value].Name, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        var prices = await Rows(query, Facet.Price).GroupBy(_ => 1).Select(g => new { Min = g.Min(x => x.PriceFrom), Max = g.Max(x => x.PriceFrom) }).FirstOrDefaultAsync(ct);
        return new ProductFiltersResponse(total, categories, await TextValuesAsync(Facet.Material, x => x.Product.Material), await TextValuesAsync(Facet.Season, x => x.Product.Season),
            sizes, colours, stateOptions, cityOptions, new FacetPriceRange(prices?.Min, prices?.Max));
    }

    public async Task<PagedResponse<ProductSummaryResponse>> ListAsync(CatalogueQuery query, CancellationToken ct)
    {
        var rows = Rows(query, Facet.None);
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
            media.FirstOrDefault(m => m.ProductId == x.Product.Id) is { } image ? ProductMediaLinks.Url(x.Product.Slug, image, ProductMediaLinks.Card) : null,
            x.Product.Moq, x.Product.Unit, x.PriceFrom, x.PriceTo, x.Product.Material, x.Product.Status)
            { AvailableStock = stock.GetValueOrDefault(x.Product.Id), UnitsSold = x.UnitsSold, ImageSrcSet = FirstImage(media, x.Product.Id) is { } first ? ProductMediaLinks.SrcSet(x.Product.Slug, first) : null, ImageLqip = FirstImage(media, x.Product.Id) is { } preview ? ProductMediaLinks.Lqip(preview) : null }).ToList(), total, query.Page, query.PageSize);
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
            ImageUrl = media.FirstOrDefault() is { } image ? ProductMediaLinks.Url(slug, image, ProductMediaLinks.Main) : null,
            ImageSrcSet = media.FirstOrDefault() is { } first ? ProductMediaLinks.SrcSet(slug, first) : null,
            ImageLqip = media.FirstOrDefault() is { } preview ? ProductMediaLinks.Lqip(preview) : null,
            Media = media.Select(x => new ProductMediaResponse(x.Id, ProductMediaLinks.Url(slug, x, ProductMediaLinks.Main), x.MediaType, x.SortOrder) { SrcSet = ProductMediaLinks.SrcSet(slug, x), Lqip = ProductMediaLinks.Lqip(x) }).ToList() };
    }

    private static ProductMedia? FirstImage(IEnumerable<ProductMedia> media, long productId) => media.FirstOrDefault(m => m.ProductId == productId);

    /// <summary>The address of a product photo. Kept here because other endpoints already call it; the rules are in ProductMediaLinks.</summary>
    public static string MediaUrl(string slug, ProductMedia media) => ProductMediaLinks.Url(slug, media);

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
