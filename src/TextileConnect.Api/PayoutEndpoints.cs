using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class PayoutEndpoints
{
    public static void MapPayoutEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seller/payouts", async (ICurrentBusiness current, MarketplaceDbContext db, bool? csv, CancellationToken ct) =>
        {
            var rows = await db.Settlements.AsNoTracking().Where(x => x.ManufacturerBusinessId == current.RequireBusinessId()).OrderByDescending(x => x.Id).Select(x => new { sellerOrderId = x.SellerOrderId, amount = x.Amount, commission = x.CommissionAmount, status = x.Status, settledAt = x.SettledAt }).ToListAsync(ct);
            if (csv == true)
            {
                var body = "sellerOrderId,amount,commission,status,settledAt\n" + string.Join("\n", rows.Select(x => $"{x.sellerOrderId},{x.amount:0.00},{x.commission:0.00},{x.status},{x.settledAt:O}"));
                return Results.Text(body, "text/csv", System.Text.Encoding.UTF8);
            }
            return Results.Ok(new { items = rows, total = rows.Sum(x => x.amount), commission = rows.Sum(x => x.commission) });
        }).RequireAuthorization("ApprovedBusiness");
    }
}
