using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Figures for one period. Gmv = what retailers paid (goods, GST, freight) for orders paid in the period; Commission = platform fee on live (not rejected or cancelled) seller orders.</summary>
public sealed record AdminPeriodStats(string Period, DateTime From, decimal Gmv, int Orders, decimal Commission, int NewBusinesses);

/// <summary>Things that need an admin to decide right now.</summary>
public sealed record AdminDecisionCounts(int PendingApprovals, int OpenClaims, int PendingProducts, int RefundsWaiting);

public sealed record AdminDashboardResponse(AdminPeriodStats Today, AdminPeriodStats Last7Days, AdminPeriodStats Last30Days, AdminDecisionCounts NeedsDecision);

public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/dashboard", async (MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var today = now.Date;
            async Task<AdminPeriodStats> StatsAsync(string period, DateTime from)
            {
                // Paid orders are those whose payment was captured in the period; a fully refunded order is not revenue.
                var paid = db.Payments.AsNoTracking().Where(p => p.Status == "Paid" && p.PaidAt >= from && p.PaidAt <= now)
                    .Join(db.Orders.Where(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "PartiallyRefunded"), p => p.OrderId, o => o.Id, (p, o) => o);
                var live = db.SellerOrders.AsNoTracking().Where(s => s.Status != "Rejected" && s.Status != "Cancelled" && paid.Select(o => o.Id).Contains(s.OrderId));
                return new AdminPeriodStats(period, from, await paid.SumAsync(o => (decimal?)o.GrandTotal, ct) ?? 0m, await paid.CountAsync(ct),
                    await live.SumAsync(s => (decimal?)s.CommissionAmount, ct) ?? 0m,
                    await db.Businesses.AsNoTracking().CountAsync(b => b.CreatedAtUtc >= from && !b.IsDeleted, ct));
            }
            var needs = new AdminDecisionCounts(
                await db.Businesses.AsNoTracking().CountAsync(b => b.VerificationStatus == VerificationStatus.Pending && !b.IsDeleted, ct),
                await db.ReturnClaims.AsNoTracking().CountAsync(c => c.ResolvedAtUtc == null, ct),
                await db.Products.AsNoTracking().CountAsync(p => p.Status == Domain.Catalogue.ProductStatus.PendingReview && !p.IsDeleted, ct),
                await db.Refunds.AsNoTracking().CountAsync(r => r.Status == "Requested", ct));
            return Results.Ok(new AdminDashboardResponse(await StatsAsync("today", today), await StatsAsync("7d", today.AddDays(-6)), await StatsAsync("30d", today.AddDays(-29)), needs));
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
