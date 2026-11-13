using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Orders;

public sealed class EfCartService(MarketplaceDbContext db) : ICartService
{
    public async Task<IReadOnlyList<CartItem>> ListAsync(long businessId, CancellationToken cancellationToken = default)
        => await db.CartItems.Where(x => x.RetailerBusinessId == businessId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

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
