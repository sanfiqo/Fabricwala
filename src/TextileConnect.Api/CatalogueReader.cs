using TextileConnect.Application.Catalogue;
using TextileConnect.Domain;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed partial class CatalogueReader(MarketplaceDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache? cache = null)
{
    public IQueryable<Product> PublicProducts => PublicProductsOf(db.Products);

    private IQueryable<Product> PublicProductsOf(IQueryable<Product> source) => source.AsNoTracking().Where(p => !p.IsDeleted && p.Status == ProductStatus.Active
        && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved));

    private bool IsMySql => db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The filters a shopper can set. A facet count is worked out with every filter except its own, so the shopper sees what else they could pick.</summary>
    private enum Facet { None, Category, Material, Season, Size, Colour, State, City, Price }

    /// <summary>
    /// Where each seller is, worked out once per request from a few small tables, so that no query has to look an address up per product.
    /// The address that counts for a seller is the pickup address before the registered one, the default one first, then the oldest.
    /// </summary>
    private sealed class Geo
    {
        public required Dictionary<long, long> SellerCity { get; init; }
        public required Dictionary<long, City> Cities { get; init; }
        public required Dictionary<long, State> States { get; init; }

        public List<long> SellersInCity(long cityId) => SellerCity.Where(x => x.Value == cityId).Select(x => x.Key).ToList();

        public List<long> SellersInState(Func<State, bool> match)
            => SellerCity.Where(x => Cities.TryGetValue(x.Value, out var city) && States.TryGetValue(city.StateId, out var state) && match(state)).Select(x => x.Key).ToList();
    }

    private async Task<Geo> LoadGeoAsync(IReadOnlyCollection<long>? sellerIds, CancellationToken ct)
    {
        var addresses = db.BusinessAddresses.AsNoTracking().Where(a => a.AddressType == AddressType.Pickup || a.AddressType == AddressType.Registered);
        addresses = sellerIds is null
            ? addresses.Where(a => db.Businesses.Any(b => b.Id == a.BusinessId && b.BusinessType == BusinessType.Manufacturer))
            : addresses.Where(a => sellerIds.Contains(a.BusinessId));
        var rows = await addresses.Select(a => new { a.BusinessId, a.CityId, a.AddressType, a.IsDefault, a.Id }).ToListAsync(ct);
        var sellerCity = rows.GroupBy(a => a.BusinessId).ToDictionary(g => g.Key,
            g => g.OrderBy(a => a.AddressType == AddressType.Pickup ? 0 : 1).ThenByDescending(a => a.IsDefault).ThenBy(a => a.Id).First().CityId);
        return new Geo
        {
            SellerCity = sellerCity,
            Cities = await db.Cities.AsNoTracking().ToDictionaryAsync(x => x.Id, ct),
            States = await db.States.AsNoTracking().ToDictionaryAsync(x => x.Id, ct),
        };
    }

    private static bool NeedsGeo(CatalogueQuery query) => query.StateId.HasValue || !string.IsNullOrWhiteSpace(query.State) || query.SellerCityId.HasValue;

    /// <summary>
    /// The products that match the query, with the fields the list sorts and filters on. Facet says which filter to leave out.
    /// ForCount picks the shape that is fastest for counting and grouping (every product is looked at anyway, so a semi-join is best); the other shape, used for a page,
    /// asks about the seller and the price slabs one product at a time, which lets MySQL walk the products in order and stop after the first page. Both mean the same.
    /// </summary>
    private IQueryable<CatalogueRow> Rows(CatalogueQuery query, Facet skip, Geo? geo, bool forCount)
    {
        var search = ProductTextSearch.For(query.Q);
        var useFullText = search.BooleanQuery is not null && IsMySql;
        // On MySQL a plain word search goes through the full-text index (FTX_Products_Search, migration AddProductFullText). The SELECT * is the same table, so every other filter still applies on top of it.
        var source = useFullText
            ? db.Products.FromSqlInterpolated($"SELECT * FROM `Products` WHERE MATCH(`Title`, `Description`, `Material`) AGAINST ({search.BooleanQuery} IN BOOLEAN MODE)")
            : db.Products;
        var products = source.AsNoTracking().Where(p => !p.IsDeleted && p.Status == ProductStatus.Active);
        // The cheap tests on the product row come first and the questions about other tables last: MySQL tests an AND from the left and stops at the first false,
        // so a search for "xl" is not preceded by 20,000 lookups of the seller and the price slabs.
        var text = search.Like;
        if (!string.IsNullOrEmpty(text) && !useFullText)
            products = products.Where(x => x.Title.ToLower().Contains(text) || x.Description != null && x.Description.ToLower().Contains(text) || x.Material != null && x.Material.ToLower().Contains(text));
        if (query.CategoryId.HasValue && skip != Facet.Category) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (query.MaxMoq.HasValue) products = products.Where(x => x.Moq <= query.MaxMoq);
        if (query.MinPrice.HasValue && skip != Facet.Price) products = products.Where(x => x.PriceFrom >= query.MinPrice);
        if (query.MaxPrice.HasValue && skip != Facet.Price) products = products.Where(x => x.PriceFrom <= query.MaxPrice);
        if (!string.IsNullOrWhiteSpace(query.Material) && skip != Facet.Material) products = products.Where(x => x.Material != null && x.Material.Contains(query.Material));
        if (!string.IsNullOrWhiteSpace(query.Season) && skip != Facet.Season) products = products.Where(x => x.Season != null && x.Season.Contains(query.Season));
        if (query.StateId.HasValue && skip != Facet.State) { var sellers = geo!.SellersInState(s => s.Id == query.StateId); products = products.Where(x => sellers.Contains(x.SellerBusinessId)); }
        if (!string.IsNullOrWhiteSpace(query.State) && skip != Facet.State)
        {
            var sellers = geo!.SellersInState(s => string.Equals(s.Code, query.State, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Name, query.State, StringComparison.OrdinalIgnoreCase));
            products = products.Where(x => sellers.Contains(x.SellerBusinessId));
        }
        if (query.SellerCityId.HasValue && skip != Facet.City) { var sellers = geo!.SellersInCity(query.SellerCityId.Value); products = products.Where(x => sellers.Contains(x.SellerBusinessId)); }
        if (!string.IsNullOrWhiteSpace(query.Size) && skip != Facet.Size) products = products.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Id && v.IsActive && v.Size == query.Size));
        if (!string.IsNullOrWhiteSpace(query.Colour) && skip != Facet.Colour) products = products.Where(x => db.ProductVariants.Any(v => v.ProductId == x.Id && v.IsActive && v.Colour == query.Colour));
        // A product is listed when its seller is approved and it has at least one price tier, which is the same as having a lowest price (Product.PriceFrom).
        products = forCount
            ? products.Where(p => p.PriceFrom != null && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved))
            : products.Where(p => p.PriceFrom != null && db.Businesses.Count(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved) > 0);
        var rows = products.Select(product => new CatalogueRow { Product = product, PriceFrom = product.PriceFrom!.Value, PriceTo = product.PriceTo!.Value, UnitsSold = product.UnitsSold });
        return rows;
    }

    private const int MaxFacetValues = 50;

    /// <summary>
    /// How many products each filter value would give, for the filter sheet. Takes the same parameters as the list (paging and sort are ignored).
    /// Counted in memory from one copy of the catalogue (CatalogueFacetSnapshot.cs); a catalogue too large for that is counted by the database.
    /// </summary>
    public async Task<ProductFiltersResponse> FacetsAsync(CatalogueQuery query, CancellationToken ct)
        => await FacetsFromSnapshotAsync(query, ct) ?? await FacetsFromDatabaseAsync(query, ct);

    /// <summary>The same counts, one SQL query per filter. Slower on a large catalogue; the rules every count follows are written here and in <c>Rows</c>.</summary>
    public async Task<ProductFiltersResponse> FacetsFromDatabaseAsync(CatalogueQuery query, CancellationToken ct)
    {
        var geo = await LoadGeoAsync(null, ct);
        IQueryable<CatalogueRow> Facets(Facet skip) => Rows(query, skip, geo, forCount: true);
        var total = await Facets(Facet.None).CountAsync(ct);

        var categoryCounts = await Facets(Facet.Category).GroupBy(x => x.Product.CategoryId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var categoryIds = categoryCounts.Select(x => x.Id).ToArray();
        var categoryNames = await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var categories = categoryCounts.Select(x => new FacetId(x.Id, categoryNames.GetValueOrDefault(x.Id) ?? string.Empty, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        async Task<List<FacetValue>> TextValuesAsync(Facet facet, System.Linq.Expressions.Expression<Func<CatalogueRow, string?>> column)
            => (await Facets(facet).Select(column).Where(x => x != null).GroupBy(x => x!).Select(g => new { Value = g.Key, Count = g.Count() }).ToListAsync(ct))
                .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        // Sizes and colours live on the variants. A product counts once for a size even when it has several colours of it.
        var sizeProducts = Facets(Facet.Size).Select(x => x.Product.Id);
        var sizes = (await db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Size != null && sizeProducts.Contains(v.ProductId))
                .GroupBy(v => v.Size!).Select(g => new { Value = g.Key, Count = g.Select(v => v.ProductId).Distinct().Count() }).ToListAsync(ct))
            .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();
        var colourProducts = Facets(Facet.Colour).Select(x => x.Product.Id);
        var colours = (await db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Colour != null && colourProducts.Contains(v.ProductId))
                .GroupBy(v => v.Colour!).Select(g => new { Value = g.Key, Count = g.Select(v => v.ProductId).Distinct().Count() }).ToListAsync(ct))
            .Select(x => new FacetValue(x.Value, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        // Cities and states: count per seller in the database, and turn the seller into its city here.
        async Task<List<(long CityId, int Count)>> CityCountsAsync(Facet facet)
            => (await Facets(facet).GroupBy(x => x.Product.SellerBusinessId).Select(g => new { Seller = g.Key, Count = g.Count() }).ToListAsync(ct))
                .Where(x => geo.SellerCity.ContainsKey(x.Seller)).GroupBy(x => geo.SellerCity[x.Seller]).Select(g => (g.Key, g.Sum(x => x.Count))).ToList();
        var cityCountsForState = await CityCountsAsync(Facet.State);
        var cityCounts = await CityCountsAsync(Facet.City);
        var stateOptions = cityCountsForState.Where(x => geo.Cities.ContainsKey(x.CityId))
            .GroupBy(x => geo.Cities[x.CityId].StateId).Select(g => new FacetState(g.Key, geo.States[g.Key].Name, geo.States[g.Key].Code, g.Sum(x => x.Count)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();
        var cityOptions = cityCounts.Where(x => geo.Cities.ContainsKey(x.CityId))
            .Select(x => new FacetId(x.CityId, geo.Cities[x.CityId].Name, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        var prices = await Facets(Facet.Price).GroupBy(_ => 1).Select(g => new { Min = g.Min(x => x.PriceFrom), Max = g.Max(x => x.PriceFrom) }).FirstOrDefaultAsync(ct);
        return new ProductFiltersResponse(total, categories, await TextValuesAsync(Facet.Material, x => x.Product.Material), await TextValuesAsync(Facet.Season, x => x.Product.Season),
            sizes, colours, stateOptions, cityOptions, new FacetPriceRange(prices?.Min, prices?.Max));
    }

    public async Task<PagedResponse<ProductSummaryResponse>> ListAsync(CatalogueQuery query, CancellationToken ct)
    {
        var geo = NeedsGeo(query) ? await LoadGeoAsync(null, ct) : null;
        var total = await Rows(query, Facet.None, geo, forCount: true).CountAsync(ct);
        var rows = Rows(query, Facet.None, geo, forCount: false);
        var sorted = query.Sort?.ToLowerInvariant() switch
        {
            "popular" => rows.OrderByDescending(x => x.UnitsSold).ThenByDescending(x => x.Product.Id),
            "price-asc" => rows.OrderBy(x => x.PriceFrom).ThenBy(x => x.Product.Id),
            "price-desc" => rows.OrderByDescending(x => x.PriceFrom).ThenBy(x => x.Product.Id),
            "moq-asc" => rows.OrderBy(x => x.Product.Moq).ThenBy(x => x.Product.Id),
            _ => rows.OrderByDescending(x => x.Product.Id)
        };
        // Sort and cut the page on narrow rows (the id and the numbers that are sorted by), then read the products of the page. Sorting 20,000 products with all their columns,
        // descriptions included, is what made a sort by price slow.
        var slice = await sorted.Select(x => new { x.Product.Id, x.PriceFrom, x.PriceTo, x.UnitsSold }).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var sliceIds = slice.Select(x => x.Id).ToArray();
        var loaded = await db.Products.AsNoTracking().Where(x => sliceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var page = slice.Where(x => loaded.ContainsKey(x.Id)).Select(x => new CatalogueRow { Product = loaded[x.Id], PriceFrom = x.PriceFrom, PriceTo = x.PriceTo, UnitsSold = x.UnitsSold }).ToList();
        var ids = page.Select(x => x.Product.Id).ToArray();
        var sellerIds = page.Select(x => x.Product.SellerBusinessId).Distinct().ToArray();
        var sellerNames = await db.Businesses.AsNoTracking().Where(x => sellerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var pageGeo = geo ?? await LoadGeoAsync(sellerIds, ct);
        string CityOf(long sellerId) => pageGeo.SellerCity.TryGetValue(sellerId, out var cityId) && pageGeo.Cities.TryGetValue(cityId, out var city) ? city.Name : string.Empty;
        var media = await db.ProductMedia.AsNoTracking().Where(x => ids.Contains(x.ProductId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var stock = await db.ProductVariants.AsNoTracking().Where(x => ids.Contains(x.ProductId) && x.IsActive).GroupBy(x => x.ProductId).Select(x => new { Id = x.Key, Quantity = x.Sum(v => v.StockQty) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        return new(page.Select(x => new ProductSummaryResponse(x.Product.Id, x.Product.Slug, x.Product.Title, x.Product.CategoryId,
            x.Product.SellerBusinessId, sellerNames.GetValueOrDefault(x.Product.SellerBusinessId) ?? string.Empty, CityOf(x.Product.SellerBusinessId),
            media.FirstOrDefault(m => m.ProductId == x.Product.Id) is { } image ? ProductMediaLinks.Url(x.Product.Slug, image, ProductMediaLinks.Card) : null,
            x.Product.Moq, x.Product.Unit, x.PriceFrom, x.PriceTo, x.Product.Material, x.Product.Status)
            { AvailableStock = stock.GetValueOrDefault(x.Product.Id), UnitsSold = x.UnitsSold, ImageSrcSet = FirstImage(media, x.Product.Id) is { } first ? ProductMediaLinks.SrcSet(x.Product.Slug, first) : null,
              ImageLqip = FirstImage(media, x.Product.Id) is { } preview ? ProductMediaLinks.Lqip(preview) : null }).ToList(), total, query.Page, query.PageSize);
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
        public decimal PriceFrom { get; init; }
        public decimal PriceTo { get; init; }
        public int UnitsSold { get; init; }
    }
}
