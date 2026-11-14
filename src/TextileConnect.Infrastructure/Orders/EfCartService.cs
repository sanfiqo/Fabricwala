using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfCartService(MarketplaceDbContext db) : ICartService
{
    public async Task<IReadOnlyList<CartItem>> ListAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.CartItems.Where(x => x.RetailerBusinessId == businessId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

    public async Task<CartView> GetViewAsync(long businessId, CancellationToken cancellationToken = default)
    {
        var items = await db.CartItems.AsNoTracking().Where(x => x.RetailerBusinessId == businessId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (items.Count == 0) return new CartView([]);
        var variantIds = items.Select(x => x.VariantId).ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Where(x => variantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var productIds = variants.Values.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var sellerIds = products.Values.Select(x => x.SellerBusinessId).Distinct().ToArray();
        var sellers = await db.Businesses.AsNoTracking().Where(x => sellerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var slabs = (await db.PriceSlabs.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(cancellationToken)).ToLookup(x => x.ProductId);
        var media = (await db.ProductMedia.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken))
            .GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());

        bool Purchasable(CartItem item, out ProductVariant variant, out Product product)
        {
            variant = null!; product = null!;
            if (!variants.TryGetValue(item.VariantId, out var v) || !v.IsActive || !products.TryGetValue(v.ProductId, out var p)) return false;
            variant = v; product = p;
            return !p.IsDeleted && p.Status == ProductStatus.Active && sellers.TryGetValue(p.SellerBusinessId, out var seller)
                && !seller.IsDeleted && seller.BusinessType == BusinessType.Manufacturer && seller.VerificationStatus == VerificationStatus.Approved;
        }

        // Price tier and MOQ apply to the product's total quantity across sizes and colours.
        var productQuantity = items.Where(x => Purchasable(x, out _, out _))
            .GroupBy(x => variants[x.VariantId].ProductId).ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity));
        var lines = new List<(long Seller, string SellerName, CartLine Line)>();
        foreach (var item in items)
        {
            if (!Purchasable(item, out var variant, out var product))
            {
                variants.TryGetValue(item.VariantId, out var oldVariant);
                var oldProduct = oldVariant is null ? null : products.GetValueOrDefault(oldVariant.ProductId);
                var oldSeller = oldProduct is null ? null : sellers.GetValueOrDefault(oldProduct.SellerBusinessId);
                lines.Add((oldProduct?.SellerBusinessId ?? 0, oldSeller?.DisplayName ?? string.Empty, new CartLine(item.Id, item.VariantId, item.Quantity, 0m, 0m, null, 0m)
                {
                    ProductId = oldProduct?.Id ?? 0, Slug = oldProduct?.Slug ?? string.Empty, Title = oldProduct?.Title ?? string.Empty,
                    Size = oldVariant?.Size, Colour = oldVariant?.Colour, Moq = oldProduct?.Moq ?? 0, ProductQuantity = item.Quantity, Issues = ["unavailable"],
                }));
                continue;
            }
            var qty = productQuantity[product.Id];
            var productSlabs = slabs[product.Id].OrderBy(x => x.MinQty).ThenBy(x => x.Id).ToList();
            // Below the first tier the lowest tier price is shown; the line is flagged below_moq instead.
            var slab = PriceSlabRules.Select(productSlabs, qty) ?? (qty < product.Moq ? productSlabs.FirstOrDefault() : null);
            var price = slab?.PricePerUnit ?? 0m;
            var subTotal = ProductQuote.Money(price * item.Quantity);
            var gst = ProductQuote.Money(subTotal * product.GstRate / 100m);
            var next = productSlabs.FirstOrDefault(x => x.MinQty > qty && x.PricePerUnit < price);
            var issues = new List<string>();
            if (slab is null) issues.Add("unavailable");
            if (qty < product.Moq) issues.Add("below_moq");
            if (item.Quantity > variant.StockQty) issues.Add("insufficient_stock");
            var image = media.GetValueOrDefault(product.Id);
            lines.Add((product.SellerBusinessId, sellers[product.SellerBusinessId].DisplayName,
                new CartLine(item.Id, item.VariantId, item.Quantity, price, ProductQuote.Money(price * product.GstRate / 100m), null, subTotal + gst)
                {
                    ProductId = product.Id, Slug = product.Slug, Title = product.Title, Size = variant.Size, Colour = variant.Colour, Moq = product.Moq, Unit = product.Unit,
                    ImageUrl = image is null ? null : MediaUrl(product.Slug, image),
                    GstRate = product.GstRate, ProductQuantity = qty, LineSubTotal = subTotal, LineGst = gst, AvailableStock = variant.StockQty, Issues = issues,
                    NextTier = next is null ? null : new CartNextTier(next.MinQty, next.PricePerUnit, next.MinQty - qty, price - next.PricePerUnit,
                        ProductQuote.Money((price - next.PricePerUnit) * next.MinQty)),
                }));
        }
        var groups = lines.GroupBy(x => x.Seller).Select(group =>
        {
            var groupLines = group.Select(x => x.Line).ToList();
            var subtotal = groupLines.Sum(x => x.LineSubTotal);
            var gst = groupLines.Sum(x => x.LineGst);
            return new CartSellerGroup(group.Key, groupLines, subtotal, gst, null, subtotal + gst) { ManufacturerName = group.First().SellerName };
        }).OrderBy(x => x.SellerBusinessId).ToList();
        return new CartView(groups);
    }

    private static string MediaUrl(string slug, ProductMedia media)
        => Uri.TryCreate(media.FileKey, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
            ? uri.AbsoluteUri : $"/api/v1/products/{Uri.EscapeDataString(slug)}/media/{media.Id}";

    public async Task<CartItem> AddItemAsync(long businessId, string userId, long variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var existing = await db.CartItems.Where(x => x.RetailerBusinessId == businessId && x.VariantId == variantId)
            .Select(x => (int?)x.Quantity).SingleOrDefaultAsync(cancellationToken);
        await ValidateAsync(variantId, (existing ?? 0) + (long)quantity, cancellationToken);
        return await AddAsync(businessId, userId, variantId, quantity, cancellationToken);
    }

    private async Task ValidateAsync(long variantId, long totalQuantity, CancellationToken cancellationToken)
    {
        if (totalQuantity is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException("quantity", "Quantity must be between 1 and 1,000,000.");
        var stock = await (from variant in db.ProductVariants.AsNoTracking()
                           join product in db.Products.AsNoTracking() on variant.ProductId equals product.Id
                           where variant.Id == variantId && variant.IsActive && !product.IsDeleted && product.Status == ProductStatus.Active
                               && db.Businesses.Any(b => b.Id == product.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved)
                           select (int?)variant.StockQty).SingleOrDefaultAsync(cancellationToken);
        if (stock is null) throw new KeyNotFoundException("Product variant not available.");
        if (totalQuantity > stock) throw new ArgumentException("Quantity exceeds available stock.");
    }

    public async Task<CartItem> AddAsync(long businessId, string userId, long variantId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var item = await db.CartItems.SingleOrDefaultAsync(x => x.RetailerBusinessId == businessId && x.VariantId == variantId, cancellationToken);
        if (item is null)
        {
            item = new CartItem { RetailerBusinessId = businessId, AddedByUserId = userId, VariantId = variantId, Quantity = quantity };
            db.CartItems.Add(item);
        }
        else item.Quantity += quantity;
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<CartItem?> UpdateAsync(long businessId, long itemId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var item = await db.CartItems.SingleOrDefaultAsync(x => x.Id == itemId && x.RetailerBusinessId == businessId, cancellationToken);
        if (item is null) return null;
        await ValidateAsync(item.VariantId, quantity, cancellationToken);
        item.Quantity = quantity;
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> RemoveAsync(long businessId, long itemId, CancellationToken cancellationToken = default)
    {
        var item = await db.CartItems.SingleOrDefaultAsync(x => x.Id == itemId && x.RetailerBusinessId == businessId, cancellationToken);
        if (item is null) return false;
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
