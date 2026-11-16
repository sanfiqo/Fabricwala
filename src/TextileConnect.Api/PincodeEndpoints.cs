using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class PincodeEndpoints
{
    public static void MapPincodeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/pincodes/{pincode}", async (string pincode, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!BusinessInputRules.IsPincode(pincode)) return Results.BadRequest();
            var result = await (from postal in db.PostalCodes.AsNoTracking()
                                join city in db.Cities on postal.CityId equals city.Id
                                join state in db.States on city.StateId equals state.Id
                                where postal.Pincode == pincode
                                select new PincodeResponse(postal.Pincode, city.Id, city.Name, state.Id, state.Name)).SingleOrDefaultAsync(ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // Lookup lists for the signup fallback (pincode not found) and the admin freight screen. Public master data, like the pincode lookup.
        app.MapGet("/api/v1/states", async (MarketplaceDbContext db, CancellationToken ct) =>
            Results.Ok(await db.States.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new StateResponse(x.Id, x.Name, x.Code)).ToListAsync(ct)));

        app.MapGet("/api/v1/cities", async (long? stateId, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var query = db.Cities.AsNoTracking().AsQueryable();
            if (stateId is not null) query = query.Where(x => x.StateId == stateId);
            return Results.Ok(await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new CityResponse(x.Id, x.StateId, x.Name, x.IsDeliveryZone, x.IsSourcingHub)).ToListAsync(ct));
        });
    }
}
