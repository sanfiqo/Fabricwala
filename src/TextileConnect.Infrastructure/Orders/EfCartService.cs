using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfCartService(MarketplaceDbContext db) : ICartService
{
    public async Task<IReadOnlyList<CartItem>> ListAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.CartItems.Where(x => x.RetailerBusinessId == businessId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

    public async Task<CartView> GetViewAsync(long businessId, CancellationToken cancellationToken = default)
    {
        var rows = await (from item in db.CartItems
                          join variant in db.ProductVariants on item.VariantId equals variant.Id
                          join product in db.Products on variant.ProductId equals product.Id
                          join slab in db.PriceSlabs on product.Id equals slab.ProductId into slabs
                          where item.RetailerBusinessId == businessId && !product.IsDeleted
                          select new { item, product, slabs }).ToListAsync(cancellationToken);
        var lines = rows.Select(row =>
        {
            var slab = PriceSlabRules.Select(row.slabs, row.item.Quantity);
            var price = slab?.PricePerUnit ?? 0m;
            var gst = price * row.product.GstRate;
            return new { Seller = row.product.SellerBusinessId, Line = new CartLine(row.item.Id, row.item.VariantId, row.item.Quantity, price, gst, null, (price + gst) * row.item.Quantity) };
        }).GroupBy(x => x.Seller).Select(group =>
        {
            var items = group.Select(x => x.Line).ToList();
            var subtotal = items.Sum(x => x.SlabPrice * x.Quantity);
            var gst = items.Sum(x => x.GstPerUnit * x.Quantity);
            return new CartSellerGroup(group.Key, items, subtotal, gst, null, subtotal + gst);
        }).ToList();
        return new CartView(lines);
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
