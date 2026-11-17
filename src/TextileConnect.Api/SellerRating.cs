using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>A seller's rating: the average of the stars retailers gave to delivered orders that contain the seller, to one decimal. A rated order counts for every seller in it.</summary>
public static class SellerRating
{
    public static async Task<(decimal? Average, int Count)> ForAsync(MarketplaceDbContext db, long sellerId, CancellationToken ct)
    {
        var stars = await db.OrderRatings.AsNoTracking().Where(r => db.SellerOrders.Any(s => s.OrderId == r.OrderId && s.ManufacturerBusinessId == sellerId)).Select(r => r.Stars).ToListAsync(ct);
        return stars.Count == 0 ? (null, 0) : (decimal.Round((decimal)stars.Average(), 1, MidpointRounding.AwayFromZero), stars.Count);
    }
}
