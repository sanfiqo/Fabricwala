using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Api;

/// <summary>
/// The filter counts worked out in memory from a small copy of the listed products (id, category, seller, material, season, MOQ, lowest price, and the sizes and colours of the active variants).
/// Counting every facet with its own SQL query read the whole catalogue nine times; this reads it once, and when the host has a memory cache the copy is kept for a short time
/// (<see cref="SnapshotLifetime"/>), so the counts on the filter sheet can be up to that old. The rules are the ones of <c>Rows</c>; a test compares the two on the same data.
/// A catalogue larger than <see cref="SnapshotLimit"/> products is not copied: the counts then come from the database as before.
/// </summary>
public sealed partial class CatalogueReader
{
    public const int SnapshotLimit = 50_000;
    public static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(30);
    private const string SnapshotKey = "catalogue-facet-snapshot-v1";

    private sealed record SnapProduct(long Id, long CategoryId, long SellerId, string? Material, string? Season, int Moq, decimal PriceFrom);

    private sealed class Snapshot
    {
        public required SnapProduct[] Products { get; init; }
        public required Dictionary<long, (string? Size, string? Colour)[]> Variants { get; init; }
    }

    private async Task<Snapshot?> GetSnapshotAsync(CancellationToken ct)
        => cache is null ? await BuildSnapshotAsync(ct)
            : await cache.GetOrCreateAsync(SnapshotKey, async entry => { entry.AbsoluteExpirationRelativeToNow = SnapshotLifetime; return await BuildSnapshotAsync(ct); });

    private async Task<Snapshot?> BuildSnapshotAsync(CancellationToken ct)
    {
        var sellerIds = await db.Businesses.AsNoTracking()
            .Where(b => !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved).Select(b => b.Id).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(p => !p.IsDeleted && p.Status == ProductStatus.Active && sellerIds.Contains(p.SellerBusinessId))
            .Select(p => new { p.Id, p.CategoryId, p.SellerBusinessId, p.Material, p.Season, p.Moq }).Take(SnapshotLimit + 1).ToListAsync(ct);
        if (products.Count > SnapshotLimit) return null;
        var prices = await db.PriceSlabs.AsNoTracking().GroupBy(s => s.ProductId).Select(g => new { ProductId = g.Key, Min = g.Min(s => s.PricePerUnit) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Min, ct);
        var listed = products.Where(p => prices.ContainsKey(p.Id)).Select(p => new SnapProduct(p.Id, p.CategoryId, p.SellerBusinessId, p.Material, p.Season, p.Moq, prices[p.Id])).ToArray();
        var listedIds = listed.Select(p => p.Id).ToHashSet();
        var variants = (await db.ProductVariants.AsNoTracking().Where(v => v.IsActive).Select(v => new { v.ProductId, v.Size, v.Colour }).ToListAsync(ct))
            .Where(v => listedIds.Contains(v.ProductId)).GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.Select(v => (v.Size, v.Colour)).ToArray());
        return new Snapshot { Products = listed, Variants = variants };
    }

    /// <summary>The ids of the products that match the words of the search, or null when the query has no words.</summary>
    private async Task<HashSet<long>?> TextMatchesAsync(CatalogueQuery query, CancellationToken ct)
    {
        var search = ProductTextSearch.For(query.Q);
        var useFullText = search.BooleanQuery is not null && IsMySql;
        var text = search.Like;
        if (!useFullText && string.IsNullOrEmpty(text)) return null;
        var source = useFullText
            ? db.Products.FromSqlInterpolated($"SELECT * FROM `Products` WHERE MATCH(`Title`, `Description`, `Material`) AGAINST ({search.BooleanQuery} IN BOOLEAN MODE)")
            : db.Products;
        var matches = source.AsNoTracking();
        if (!useFullText)
            matches = matches.Where(x => x.Title.ToLower().Contains(text!) || x.Description != null && x.Description.ToLower().Contains(text!) || x.Material != null && x.Material.ToLower().Contains(text!));
        return (await matches.Select(x => x.Id).ToListAsync(ct)).ToHashSet();
    }

    /// <summary>Same answer as <see cref="FacetsFromDatabaseAsync"/>, from the copy in memory. Null when the catalogue is too large to copy.</summary>
    public async Task<ProductFiltersResponse?> FacetsFromSnapshotAsync(CatalogueQuery query, CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(ct);
        if (snapshot is null) return null;
        var geo = await LoadGeoAsync(null, ct);
        var textMatches = await TextMatchesAsync(query, ct);

        var stateSellers = query.StateId.HasValue ? geo.SellersInState(s => s.Id == query.StateId).ToHashSet() : null;
        var stateNameSellers = !string.IsNullOrWhiteSpace(query.State)
            ? geo.SellersInState(s => string.Equals(s.Code, query.State, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Name, query.State, StringComparison.OrdinalIgnoreCase)).ToHashSet() : null;
        var citySellers = query.SellerCityId.HasValue ? geo.SellersInCity(query.SellerCityId.Value).ToHashSet() : null;
        static bool Has(string? value, string wanted) => value is not null && value.Contains(wanted, StringComparison.OrdinalIgnoreCase);
        bool HasVariant(long productId, Func<(string? Size, string? Colour), bool> test) => snapshot.Variants.TryGetValue(productId, out var list) && list.Any(test);

        IEnumerable<SnapProduct> Matching(Facet skip)
        {
            IEnumerable<SnapProduct> rows = snapshot.Products;
            if (textMatches is not null) rows = rows.Where(p => textMatches.Contains(p.Id));
            if (query.CategoryId.HasValue && skip != Facet.Category) rows = rows.Where(p => p.CategoryId == query.CategoryId);
            if (query.MinPrice.HasValue && skip != Facet.Price) rows = rows.Where(p => p.PriceFrom >= query.MinPrice);
            if (query.MaxPrice.HasValue && skip != Facet.Price) rows = rows.Where(p => p.PriceFrom <= query.MaxPrice);
            if (query.MaxMoq.HasValue) rows = rows.Where(p => p.Moq <= query.MaxMoq);
            if (stateSellers is not null && skip != Facet.State) rows = rows.Where(p => stateSellers.Contains(p.SellerId));
            if (stateNameSellers is not null && skip != Facet.State) rows = rows.Where(p => stateNameSellers.Contains(p.SellerId));
            if (citySellers is not null && skip != Facet.City) rows = rows.Where(p => citySellers.Contains(p.SellerId));
            if (!string.IsNullOrWhiteSpace(query.Material) && skip != Facet.Material) rows = rows.Where(p => Has(p.Material, query.Material));
            if (!string.IsNullOrWhiteSpace(query.Season) && skip != Facet.Season) rows = rows.Where(p => Has(p.Season, query.Season));
            if (!string.IsNullOrWhiteSpace(query.Size) && skip != Facet.Size) rows = rows.Where(p => HasVariant(p.Id, v => string.Equals(v.Size, query.Size, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(query.Colour) && skip != Facet.Colour) rows = rows.Where(p => HasVariant(p.Id, v => string.Equals(v.Colour, query.Colour, StringComparison.OrdinalIgnoreCase)));
            return rows;
        }

        var total = Matching(Facet.None).Count();

        var categoryCounts = Matching(Facet.Category).GroupBy(p => p.CategoryId).Select(g => new { Id = g.Key, Count = g.Count() }).ToList();
        var categoryIds = categoryCounts.Select(x => x.Id).ToArray();
        var categoryNames = await db.Categories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var categories = categoryCounts.Select(x => new FacetId(x.Id, categoryNames.GetValueOrDefault(x.Id) ?? string.Empty, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        List<FacetValue> TextValues(Facet facet, Func<SnapProduct, string?> column)
            => Matching(facet).Select(column).Where(x => x != null).GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                .Select(g => new FacetValue(g.First()!, g.Count())).OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        // A product counts once for a size even when it has several colours of it.
        List<FacetValue> VariantValues(Facet facet, Func<(string? Size, string? Colour), string?> column)
            => Matching(facet).SelectMany(p => snapshot.Variants.TryGetValue(p.Id, out var list) ? list.Select(v => (Product: p.Id, Value: column(v))).Where(x => x.Value != null).Distinct(new ProductValueComparer()) : [])
                .GroupBy(x => x.Value!, StringComparer.OrdinalIgnoreCase).Select(g => new FacetValue(g.First().Value!, g.Count()))
                .OrderByDescending(x => x.Count).ThenBy(x => x.Value, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        List<(long CityId, int Count)> CityCounts(Facet facet)
            => Matching(facet).GroupBy(p => p.SellerId).Where(g => geo.SellerCity.ContainsKey(g.Key)).GroupBy(g => geo.SellerCity[g.Key]).Select(g => (g.Key, g.Sum(x => x.Count()))).ToList();
        var stateOptions = CityCounts(Facet.State).Where(x => geo.Cities.ContainsKey(x.CityId))
            .GroupBy(x => geo.Cities[x.CityId].StateId).Select(g => new FacetState(g.Key, geo.States[g.Key].Name, geo.States[g.Key].Code, g.Sum(x => x.Count)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();
        var cityOptions = CityCounts(Facet.City).Where(x => geo.Cities.ContainsKey(x.CityId))
            .Select(x => new FacetId(x.CityId, geo.Cities[x.CityId].Name, x.Count)).OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(MaxFacetValues).ToList();

        var priced = Matching(Facet.Price).Select(p => p.PriceFrom).ToList();
        return new ProductFiltersResponse(total, categories, TextValues(Facet.Material, p => p.Material), TextValues(Facet.Season, p => p.Season),
            VariantValues(Facet.Size, v => v.Size), VariantValues(Facet.Colour, v => v.Colour), stateOptions, cityOptions,
            new FacetPriceRange(priced.Count == 0 ? null : priced.Min(), priced.Count == 0 ? null : priced.Max()));
    }

    /// <summary>Two (product, value) pairs are the same when the product is and the value is the same ignoring case.</summary>
    private sealed class ProductValueComparer : IEqualityComparer<(long Product, string? Value)>
    {
        public bool Equals((long Product, string? Value) x, (long Product, string? Value) y) => x.Product == y.Product && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((long Product, string? Value) obj) => HashCode.Combine(obj.Product, obj.Value?.ToUpperInvariant());
    }
}
