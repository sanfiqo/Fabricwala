using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Adds the seller city and the expected arrival date to each cart group.</summary>
public static class CartArrival
{
    /// <summary>
    /// SellerCity is the city of the seller's pickup address (registered address if there is no pickup address).
    /// ArrivesBy is today + the longest dispatch time among the group's products + the longest transit time to the retailer's default delivery address.
    /// It is null when the retailer has no delivery address or a freight rate is missing, because a guessed date would be worse than none.
    /// </summary>
    public static async Task<CartResponse> BuildAsync(CartView view, long retailerId, MarketplaceDbContext db, IProductPricing pricing, TimeProvider clock, CancellationToken ct)
    {
        var response = ApiResponseMappings.Cart(view);
        if (view.Sellers.Count == 0) return response;

        var sellerIds = view.Sellers.Select(x => x.SellerBusinessId).Where(x => x > 0).ToArray();
        var addresses = await (from address in db.BusinessAddresses.AsNoTracking()
                               join city in db.Cities.AsNoTracking() on address.CityId equals city.Id
                               where sellerIds.Contains(address.BusinessId) && (address.AddressType == AddressType.Pickup || address.AddressType == AddressType.Registered)
                               select new { address.BusinessId, address.AddressType, address.IsDefault, address.Id, City = city.Name }).ToListAsync(ct);
        var cityBySeller = addresses.GroupBy(x => x.BusinessId).ToDictionary(g => g.Key,
            g => g.OrderBy(x => x.AddressType == AddressType.Pickup ? 0 : 1).ThenByDescending(x => x.IsDefault).ThenBy(x => x.Id).First().City);

        var buyable = view.Sellers.SelectMany(s => s.Items).Where(i => i.ProductId > 0 && !i.Issues.Contains("unavailable")).ToList();
        var productIds = buyable.Select(x => x.ProductId).Distinct().ToArray();
        var dispatchDays = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DispatchDays, ct);

        IReadOnlyDictionary<long, ProductQuote>? quotes = null;
        try
        {
            var destination = await pricing.DestinationAsync(null, null, retailerId, ct);
            if (destination is not null && productIds.Length > 0)
                quotes = await pricing.QuoteAsync(buyable.GroupBy(x => x.ProductId).Select(g => new ProductQuantity(g.Key, g.First().ProductQuantity)).ToList(), destination, ct);
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException) { quotes = null; }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return response with
        {
            Groups = response.Groups.Select(group =>
            {
                var items = view.Sellers.First(x => x.SellerBusinessId == group.ManufacturerId).Items.Where(i => i.ProductId > 0 && !i.Issues.Contains("unavailable")).ToList();
                DateOnly? arrivesBy = null;
                if (quotes is not null && items.Count > 0 && items.All(i => quotes.TryGetValue(i.ProductId, out var quote) && quote.TransitDays.HasValue))
                    arrivesBy = today.AddDays(items.Max(i => dispatchDays.GetValueOrDefault(i.ProductId)) + items.Max(i => quotes[i.ProductId].TransitDays!.Value));
                return group with { SellerCity = cityBySeller.GetValueOrDefault(group.ManufacturerId), ArrivesBy = arrivesBy };
            }).ToList()
        };
    }
}
