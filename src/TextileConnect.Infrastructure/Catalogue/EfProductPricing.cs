using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Catalogue;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Catalogue;

public sealed class EfProductPricing(MarketplaceDbContext db) : IProductPricing
{
    public async Task<PriceDestination?> DestinationAsync(string? pincode, long? cityId, long? retailerBusinessId, CancellationToken ct = default)
    {
        if (pincode is not null && !BusinessInputRules.IsPincode(pincode) || cityId is <= 0)
            throw new ArgumentException("Invalid destination.");
        if (pincode is null && retailerBusinessId.HasValue)
        {
            var address = await db.BusinessAddresses.AsNoTracking().Where(x => x.BusinessId == retailerBusinessId && x.AddressType == AddressType.Delivery && (!cityId.HasValue || x.CityId == cityId))
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (address is not null) { pincode = address.Pincode; cityId ??= address.CityId; }
        }
        if (pincode is not null)
        {
            var postalCity = await db.PostalCodes.AsNoTracking().Where(x => x.Pincode == pincode).Select(x => (long?)x.CityId).SingleOrDefaultAsync(ct);
            if (postalCity.HasValue && cityId.HasValue && postalCity != cityId) throw new ArgumentException("Pincode and city do not match.");
            cityId ??= postalCity;
            if (!cityId.HasValue) throw new KeyNotFoundException("Pincode is not in the postal directory.");
        }
        if (!cityId.HasValue) return null;
        if (!await db.Cities.AnyAsync(x => x.Id == cityId, ct)) throw new KeyNotFoundException("City not found.");
        return new(cityId.Value, pincode);
    }

    public async Task<IReadOnlyDictionary<long, ProductQuote>> QuoteAsync(IReadOnlyCollection<ProductQuantity> products, PriceDestination destination, CancellationToken ct = default)
    {
        if (products.Count > 100 || products.Any(x => x.ProductId <= 0 || x.Quantity is < 1 or > 1_000_000)
            || products.Select(x => x.ProductId).Distinct().Count() != products.Count)
            throw new ArgumentException("Invalid product quantities.");
        if (products.Count == 0) return new Dictionary<long, ProductQuote>();
        var ids = products.Select(x => x.ProductId).ToArray();
        var items = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && !p.IsDeleted && p.Status == ProductStatus.Active
            && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.BusinessType == BusinessType.Manufacturer && b.VerificationStatus == VerificationStatus.Approved)).ToListAsync(ct);
        if (items.Count != ids.Length) throw new KeyNotFoundException("Product not available.");
        var sellerIds = items.Select(x => x.SellerBusinessId).Distinct().ToArray();
        var slabs = await db.PriceSlabs.AsNoTracking().Where(x => ids.Contains(x.ProductId)).ToListAsync(ct);
        var pickups = await db.BusinessAddresses.AsNoTracking().Where(x => sellerIds.Contains(x.BusinessId) && x.AddressType == AddressType.Pickup)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).ToListAsync(ct);
        var zones = await db.DeliveryZones.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.PincodePrefix.Length).ThenBy(x => x.Id).ToListAsync(ct);
        var targetZone = destination.Pincode is null ? null : zones.FirstOrDefault(x => destination.Pincode.StartsWith(x.PincodePrefix, StringComparison.Ordinal));
        var sourceZoneIds = pickups.Select(p => zones.FirstOrDefault(z => p.Pincode.StartsWith(z.PincodePrefix, StringComparison.Ordinal))?.Id).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var zoneRates = targetZone is null ? [] : await db.DeliveryZoneRates.AsNoTracking().Where(x => x.IsActive && sourceZoneIds.Contains(x.FromZoneId) && x.ToZoneId == targetZone.Id).ToListAsync(ct);
        var sourceCities = pickups.Select(x => x.CityId).Distinct().ToArray();
        var cityRates = await db.ShippingRates.AsNoTracking().Where(x => x.IsActive && sourceCities.Contains(x.FromCityId) && x.ToCityId == destination.CityId).ToListAsync(ct);
        var result = new Dictionary<long, ProductQuote>();
        foreach (var request in products)
        {
            var product = items.Single(x => x.Id == request.ProductId);
            if (request.Quantity < product.Moq) throw new ArgumentException("Quantity is below MOQ.");
            var slab = PriceSlabRules.Select(slabs.Where(x => x.ProductId == product.Id), request.Quantity)
                ?? throw new ArgumentException("Quantity is outside price tiers.");
            if (slab.PricePerUnit <= 0 || product.GstRate is < 0 or > 100) throw new ArgumentException("Product pricing is invalid.");
            var pickup = pickups.FirstOrDefault(x => x.BusinessId == product.SellerBusinessId);
            var sourceZone = pickup is null ? null : zones.FirstOrDefault(x => pickup.Pincode.StartsWith(x.PincodePrefix, StringComparison.Ordinal));
            var zoneRate = zoneRates.Where(x => x.FromZoneId == sourceZone?.Id).OrderBy(x => x.RateType == ShippingRateType.PerPiece ? 0 : 1).FirstOrDefault();
            var cityRate = cityRates.Where(x => x.FromCityId == pickup?.CityId).OrderBy(x => x.RateType == ShippingRateType.PerPiece ? 0 : 1).FirstOrDefault();
            var type = zoneRate?.RateType ?? cityRate?.RateType;
            var rate = zoneRate?.Rate ?? cityRate?.Rate;
            var minimum = zoneRate?.MinCharge ?? cityRate?.MinCharge;
            decimal? shipping = rate is >= 0 && minimum is >= 0 && (type == ShippingRateType.PerPiece || product.WeightPerUnitGrams > 0)
                ? ProductQuote.Money(Math.Max(rate.Value * request.Quantity * (type == ShippingRateType.PerKg ? product.WeightPerUnitGrams / 1000m : 1m), minimum.Value)) : null;
            var factory = ProductQuote.Money(slab.PricePerUnit * request.Quantity);
            result.Add(product.Id, new(product.Id, request.Quantity, destination.CityId, slab.PricePerUnit, factory,
                ProductQuote.Money(factory * product.GstRate / 100m), shipping, shipping.HasValue ? zoneRate?.TransitDays ?? cityRate?.TransitDays : null));
        }
        return result;
    }
}
