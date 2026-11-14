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
    }
}
