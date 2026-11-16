using TextileConnect.Application.Businesses;
using TextileConnect.Application.Catalogue;

namespace TextileConnect.Api;

public static class LandedCostEndpoints
{
    public static void MapLandedCostEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/landed-cost", async (long productId, int qty, long? cityId, string? pincode, IProductPricing pricing, ICurrentBusiness current, CancellationToken ct) =>
        {
            var destination = await pricing.DestinationAsync(pincode, cityId, current.BusinessId, ct);
            if (destination is null) return Results.BadRequest();
            var quotes = await pricing.QuoteAsync([new(productId, qty)], destination, ct);
            return Results.Ok(LandedCostResponse.From(quotes[productId]));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Retailer"));
    }
}
