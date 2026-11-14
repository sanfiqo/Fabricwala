using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class PayoutEndpoints
{
    public static void MapPayoutEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seller/payouts", async (string? status, int? page, int? pageSize, bool? csv, ICurrentBusiness current, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (!SellerPayoutReader.IsValidStatus(status) || number is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            var reader = new SellerPayoutReader(db, clock);
            if (csv == true)
                return Results.File(System.Text.Encoding.UTF8.GetBytes(await reader.CsvAsync(current.RequireBusinessId(), status, ct)), "text/csv; charset=utf-8", "payouts.csv");
            return Results.Ok(await reader.SummaryAsync(current.RequireBusinessId(), status, number, size, ct));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Manufacturer"));
    }
}
