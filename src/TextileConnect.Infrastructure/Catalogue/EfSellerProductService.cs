using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Catalogue;

public sealed partial class EfSellerProductService(MarketplaceDbContext db, TimeProvider? clock = null) : ISellerProductService
{
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugPattern();

    public async Task<PagedResponse<SellerProductSummary>> ListAsync(long sellerId, string? filter, string? query, int lowStockThreshold, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var products = db.Products.AsNoTracking().Where(x => x.SellerBusinessId == sellerId && !x.IsDeleted);
        products = filter switch
        {
            "draft" => products.Where(x => x.Status == ProductStatus.Draft),
            "active" => products.Where(x => x.Status == ProductStatus.Active),
            "inactive" => products.Where(x => x.Status == ProductStatus.Inactive || x.Status == ProductStatus.PendingReview),
            "low_stock" => products.Where(x => x.Status != ProductStatus.Draft && db.ProductVariants.Any(v => v.ProductId == x.Id && v.IsActive && v.StockQty <= lowStockThreshold)),
            _ => products
        };
        var search = query?.Trim();
        if (!string.IsNullOrEmpty(search))
            products = products.Where(x => x.Title.Contains(search) || db.ProductVariants.Any(v => v.ProductId == x.Id && v.Sku.Contains(search)));
        var total = await products.CountAsync(cancellationToken);
        var rows = await products.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        var variants = (await db.ProductVariants.AsNoTracking().Where(x => ids.Contains(x.ProductId)).ToListAsync(cancellationToken)).ToLookup(x => x.ProductId);
        var slabs = (await db.PriceSlabs.AsNoTracking().Where(x => ids.Contains(x.ProductId)).ToListAsync(cancellationToken)).ToLookup(x => x.ProductId);
        var media = (await db.ProductMedia.AsNoTracking().Where(x => ids.Contains(x.ProductId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken))
            .GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());
        var items = rows.Select(product =>
        {
            var active = variants[product.Id].Where(x => x.IsActive).ToList();
            var prices = slabs[product.Id].Select(x => x.PricePerUnit).ToList();
            return new SellerProductSummary(product.Id, product.Title, product.Slug, product.Status.ToString(), product.Status == ProductStatus.Active, product.Moq,
                prices.Count == 0 ? null : prices.Min(), prices.Count == 0 ? null : prices.Max(), active.Sum(x => x.StockQty),
                product.Status != ProductStatus.Draft && active.Any(x => x.StockQty <= lowStockThreshold), active.Count,
                media.TryGetValue(product.Id, out var image) ? ProductMediaLinks.Url(product.Slug, image, ProductMediaLinks.Card) : null, product.UpdatedAtUtc);
        }).ToList();
        return new PagedResponse<SellerProductSummary>(items, total, page, pageSize);
    }

    public async Task<SellerProductDetail?> GetAsync(long sellerId, long productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId && x.SellerBusinessId == sellerId && !x.IsDeleted, cancellationToken);
        return product is null ? null : await DetailAsync(product, cancellationToken);
    }

    public async Task<SellerProductDetail> CreateAsync(long sellerId, SellerProductRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request, request.Publish, await CategoryExistsAsync(request.CategoryId, cancellationToken));
        var now = Now;
        var product = new Product { SellerBusinessId = sellerId, Title = request.Title!.Trim(), Slug = await NewSlugAsync(request.Title!, cancellationToken), CreatedAtUtc = now, UpdatedAtUtc = now };
        Apply(product, request);
        product.Status = request.Publish ? ProductStatus.Active : ProductStatus.Draft;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken); // the id is needed for the variants and tiers
        await SyncVariantsAsync(product, request.Variants ?? [], cancellationToken);
        ReplaceTiers(product.Id, request.Tiers ?? [], []);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await DetailAsync(product, cancellationToken);
    }

    public async Task<SellerProductDetail?> UpdateAsync(long sellerId, long productId, SellerProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.SellerBusinessId == sellerId && !x.IsDeleted, cancellationToken);
        if (product is null) return null;
        var live = product.Status is ProductStatus.Active or ProductStatus.PendingReview;
        Validate(request, request.Publish || live, await CategoryExistsAsync(request.CategoryId, cancellationToken));
        if (request.Publish && product.AdminHold) throw HeldByAdmin();
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        Apply(product, request);
        if (request.Publish) product.Status = ProductStatus.Active;
        product.UpdatedAtUtc = Now;
        await SyncVariantsAsync(product, request.Variants ?? [], cancellationToken);
        ReplaceTiers(product.Id, request.Tiers ?? [], await db.PriceSlabs.Where(x => x.ProductId == product.Id).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await DetailAsync(product, cancellationToken);
    }

    public async Task<SellerProductDetail?> SetActiveAsync(long sellerId, long productId, bool active, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.SellerBusinessId == sellerId && !x.IsDeleted, cancellationToken);
        if (product is null) return null;
        if (active && product.Status != ProductStatus.Active)
        {
            if (product.AdminHold) throw HeldByAdmin();
            var variants = await db.ProductVariants.AsNoTracking().Where(x => x.ProductId == product.Id).ToListAsync(cancellationToken);
            var slabs = await db.PriceSlabs.AsNoTracking().Where(x => x.ProductId == product.Id).ToListAsync(cancellationToken);
            Validate(new SellerProductRequest(product.Title, product.CategoryId, product.Description, product.Material, product.FabricSpec, product.Season, product.HsnCode,
                product.WeightPerUnitGrams, product.DispatchDays, product.ReturnPolicy, product.GstRate, product.Moq, product.Unit,
                variants.Select(x => new SellerVariantInput(x.Id, x.Sku, x.Size, x.Colour, x.StockQty, x.IsActive)).ToList(),
                slabs.Select(x => new SellerTierInput(x.MinQty, x.PricePerUnit)).ToList(), true), true, await CategoryExistsAsync(product.CategoryId, cancellationToken));
            product.Status = ProductStatus.Active;
        }
        else if (!active && product.Status == ProductStatus.Active) product.Status = ProductStatus.Inactive;
        else return await DetailAsync(product, cancellationToken); // nothing to change (drafts stay drafts)
        product.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(product, cancellationToken);
    }

    private static RuleViolationException HeldByAdmin() => new("product_deactivated_by_admin", "This product was switched off by the platform. Contact support.");

    private static void Validate(SellerProductRequest request, bool publishing, bool categoryExists) => SellerProductRules.Validate(request, publishing, categoryExists);

    private async Task<bool> CategoryExistsAsync(long? categoryId, CancellationToken cancellationToken)
        => categoryId is > 0 && await db.Categories.AnyAsync(x => x.Id == categoryId && x.IsActive, cancellationToken);

    private static void Apply(Product product, SellerProductRequest request)
    {
        product.Title = request.Title!.Trim();
        product.CategoryId = request.CategoryId ?? 0;
        product.Description = Clean(request.Description);
        product.Material = Clean(request.Material);
        product.FabricSpec = Clean(request.FabricSpec);
        product.Season = Clean(request.Season);
        product.HsnCode = Clean(request.HsnCode);
        product.WeightPerUnitGrams = request.WeightPerUnitGrams;
        product.DispatchDays = request.DispatchDays;
        product.ReturnPolicy = Clean(request.ReturnPolicy);
        product.GstRate = request.GstRate;
        product.Moq = request.Moq;
        product.Unit = request.Unit;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task SyncVariantsAsync(Product product, IReadOnlyList<SellerVariantInput> inputs, CancellationToken cancellationToken)
    {
        var existing = await db.ProductVariants.Where(x => x.ProductId == product.Id).ToDictionaryAsync(x => x.Id, cancellationToken);
        var failures = new List<ValidationFailure>();
        var kept = new HashSet<long>();
        var usedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var sku = Clean(input.Sku);
            if (input.Id is { } id)
            {
                if (!existing.TryGetValue(id, out var variant)) { failures.Add(new ValidationFailure($"variants[{i}].id", "invalid_value")); continue; }
                kept.Add(id);
                variant.Size = Clean(input.Size);
                variant.Colour = Clean(input.Colour);
                variant.StockQty = input.StockQty;
                variant.IsActive = input.IsActive;
                if (sku is not null && !string.Equals(sku, variant.Sku, StringComparison.OrdinalIgnoreCase))
                {
                    if (!usedSkus.Add(sku) || await db.ProductVariants.AnyAsync(x => x.Sku == sku && x.Id != variant.Id, cancellationToken)) failures.Add(new ValidationFailure($"variants[{i}].sku", "invalid_value"));
                    else variant.Sku = sku;
                }
                else usedSkus.Add(variant.Sku);
                continue;
            }
            if (sku is not null && (!usedSkus.Add(sku) || await db.ProductVariants.AnyAsync(x => x.Sku == sku, cancellationToken)))
            { failures.Add(new ValidationFailure($"variants[{i}].sku", "invalid_value")); continue; }
            sku ??= await NewSkuAsync(product.Slug, input, usedSkus, cancellationToken);
            db.ProductVariants.Add(new ProductVariant { ProductId = product.Id, Sku = sku, Size = Clean(input.Size), Colour = Clean(input.Colour), StockQty = input.StockQty, IsActive = input.IsActive });
        }
        if (failures.Count > 0) throw new ValidationException(failures);
        // Variants are referenced by carts and orders, so ones left out are switched off, never deleted.
        foreach (var variant in existing.Values.Where(x => !kept.Contains(x.Id))) variant.IsActive = false;
    }

    private void ReplaceTiers(long productId, IReadOnlyList<SellerTierInput> tiers, IReadOnlyList<PriceSlab> existing)
    {
        db.PriceSlabs.RemoveRange(existing);
        var ordered = tiers.OrderBy(x => x.MinQty).ToList();
        for (var i = 0; i < ordered.Count; i++)
            db.PriceSlabs.Add(new PriceSlab { ProductId = productId, MinQty = ordered[i].MinQty, MaxQty = i + 1 < ordered.Count ? ordered[i + 1].MinQty - 1 : null, PricePerUnit = ordered[i].PricePerUnit });
    }

    private async Task<string> NewSlugAsync(string title, CancellationToken cancellationToken)
    {
        var baseSlug = NonSlugPattern().Replace(title.Trim().ToLowerInvariant(), "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "product";
        if (baseSlug == "filters") baseSlug = "filters-product"; // /products/filters is the filter counts route, not a product page
        if (baseSlug.Length > 80) baseSlug = baseSlug[..80].Trim('-');
        var slug = baseSlug;
        for (var n = 2; await db.Products.AnyAsync(x => x.Slug == slug, cancellationToken); n++) slug = $"{baseSlug}-{n}";
        return slug;
    }

    private async Task<string> NewSkuAsync(string slug, SellerVariantInput input, HashSet<string> used, CancellationToken cancellationToken)
    {
        string Token(string? value) => NonSlugPattern().Replace((value ?? string.Empty).ToLowerInvariant(), string.Empty).ToUpperInvariant();
        var prefix = slug.Replace("-", string.Empty).ToUpperInvariant();
        var baseSku = string.Join('-', new[] { prefix[..Math.Min(prefix.Length, 12)], Token(input.Size), Token(input.Colour) }.Where(x => x.Length > 0));
        var sku = baseSku;
        while (used.Contains(sku) || await db.ProductVariants.AnyAsync(x => x.Sku == sku, cancellationToken))
            sku = $"{baseSku}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(2))}";
        used.Add(sku);
        return sku;
    }

    private async Task<SellerProductDetail> DetailAsync(Product product, CancellationToken cancellationToken)
    {
        var variants = await db.ProductVariants.AsNoTracking().Where(x => x.ProductId == product.Id).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var slabs = await db.PriceSlabs.AsNoTracking().Where(x => x.ProductId == product.Id).OrderBy(x => x.MinQty).ToListAsync(cancellationToken);
        var media = await db.ProductMedia.AsNoTracking().Where(x => x.ProductId == product.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return new SellerProductDetail(product.Id, product.Title, product.Slug, product.Status.ToString(), product.Status == ProductStatus.Active, product.CategoryId, product.Description,
            product.Material, product.FabricSpec, product.Season, product.HsnCode, product.WeightPerUnitGrams, product.DispatchDays, product.ReturnPolicy, product.GstRate, product.Moq, product.Unit,
            variants.Select(x => new SellerVariantResponse(x.Id, x.Sku, x.Size, x.Colour, x.StockQty, x.IsActive)).ToList(),
            slabs.Select(x => new SellerTierResponse(x.MinQty, x.MaxQty, x.PricePerUnit)).ToList(), product.AdminHold, product.UpdatedAtUtc,
            media.Select((x, index) => new SellerMediaResponse(x.Id, ProductMediaLinks.Url(product.Slug, x, ProductMediaLinks.Main), x.MediaType, x.SortOrder, index == 0) { SrcSet = ProductMediaLinks.SrcSet(product.Slug, x), Lqip = ProductMediaLinks.Lqip(x) }).ToList());
    }
}
