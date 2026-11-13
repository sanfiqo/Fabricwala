using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class LandedCostEndpoints
{
    public static void MapLandedCostEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/landed-cost", async (long productId, int qty, long cityId, string? pincode, MarketplaceDbContext db, ICurrentBusiness current, CancellationToken ct) =>
        {
            if (qty <= 0) return Results.BadRequest("Quantity must be positive.");
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId && !x.IsDeleted && x.Status == ProductStatus.Active, ct);
            var city = await db.Cities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cityId, ct);
            if (product is null || city is null) return Results.NotFound();
            var slab = await db.PriceSlabs.AsNoTracking().Where(x => x.ProductId == productId).OrderBy(x => x.MinQty).ToListAsync(ct);
            var selected = PriceSlabRules.Select(slab, qty);
            if (selected is null) return Results.BadRequest("Quantity is outside the product price slabs.");

            var retailerAddress = await db.BusinessAddresses.AsNoTracking().Where(x => x.BusinessId == current.RequireBusinessId() && x.AddressType == AddressType.Delivery && (pincode == null || x.Pincode == pincode)).OrderByDescending(x => x.IsDefault).FirstOrDefaultAsync(ct);
            var targetPincode = pincode ?? retailerAddress?.Pincode;
            var pickup = await db.BusinessAddresses.AsNoTracking().Where(x => x.BusinessId == product.SellerBusinessId && x.AddressType == AddressType.Pickup).OrderByDescending(x => x.IsDefault).FirstOrDefaultAsync(ct);
            var sourceZone = pickup is null ? null : await FindZoneAsync(db, pickup.Pincode, ct);
            var targetZone = targetPincode is null ? null : await FindZoneAsync(db, targetPincode, ct);
            var rate = sourceZone is null || targetZone is null ? null : await db.DeliveryZoneRates.AsNoTracking().SingleOrDefaultAsync(x => x.FromZoneId == sourceZone.Id && x.ToZoneId == targetZone.Id && x.RateType == ShippingRateType.PerPiece && x.IsActive, ct);
            var gst = selected.PricePerUnit * product.GstRate / 100m;
            var feeSetting = await db.PlatformSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == "Retailer.PlatformFeeRate", ct);
            var feeRate = decimal.TryParse(feeSetting?.Value, out var parsedFee) ? parsedFee : 0m;
            var shipping = rate?.Rate is { } rateValue ? Math.Max(rateValue, rate.MinCharge / qty) : (decimal?)null;
        decimal? landed = shipping is null ? null : selected.PricePerUnit + shipping.Value + gst + selected.PricePerUnit * feeRate;
            return Results.Ok(new LandedCostResponse(productId, qty, cityId, selected.PricePerUnit, shipping, gst, selected.PricePerUnit * feeRate, landed, landed * qty, rate?.TransitDays));
        }).RequireAuthorization("ApprovedBusiness");
    }

    private static Task<DeliveryZone?> FindZoneAsync(MarketplaceDbContext db, string pincode, CancellationToken ct)
        => db.DeliveryZones.AsNoTracking().Where(x => x.IsActive && pincode.StartsWith(x.PincodePrefix)).OrderByDescending(x => x.PincodePrefix.Length).FirstOrDefaultAsync(ct);
}

public sealed record LandedCostResponse(long ProductId, int Qty, long CityId, decimal SlabPrice, decimal? ShippingPerUnit, decimal GstPerUnit, decimal PlatformFeePerUnit, decimal? LandedPerUnit, decimal? Total, int? TransitDays);
