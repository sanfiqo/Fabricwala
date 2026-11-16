using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Payout status for a seller order: OnHold (no date yet or open claim), Scheduled (release date known), Paid or Cancelled.</summary>
public sealed record SellerPayoutRow(long SellerOrderId, string OrderNumber, decimal OrderValue, decimal PlatformFee, decimal Amount, string Status, DateTime? ReleaseDate, DateTime? PaidAt)
{
    /// <summary>Filled for every row; useful on the admin payout list.</summary>
    public string SellerName { get; init; } = string.Empty;
}

/// <summary>NextPayout is the scheduled money and its earliest date. Bank account details are never part of any payout response.</summary>
public sealed record SellerPayoutSummaryResponse(decimal NextPayoutAmount, DateTime? NextPayoutDate, decimal OnHoldAmount, decimal PaidThisMonth, PagedResponse<SellerPayoutRow> Payouts);

public sealed class SellerPayoutReader(MarketplaceDbContext db, TimeProvider clock)
{
    public static bool IsValidStatus(string? status) => status is null or "all" or "on_hold" or "scheduled" or "paid" or "cancelled";

    public async Task<SellerPayoutSummaryResponse> SummaryAsync(long? sellerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var mine = db.Settlements.AsNoTracking().Where(x => sellerId == null || x.ManufacturerBusinessId == sellerId);
        var pending = mine.Where(x => x.Status == "Pending");
        var scheduled = pending.Where(x => x.ReleaseAfter < Settlements.NoReleaseDate).Where(x => !db.ReturnClaims.Any(c => c.SellerOrderId == x.SellerOrderId && c.ResolvedAtUtc == null));
        var nextAmount = await scheduled.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var nextDate = await scheduled.MinAsync(x => (DateTime?)x.ReleaseAfter, ct);
        var onHold = await pending.Where(x => x.ReleaseAfter >= Settlements.NoReleaseDate || db.ReturnClaims.Any(c => c.SellerOrderId == x.SellerOrderId && c.ResolvedAtUtc == null)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var paid = await mine.Where(x => x.Status == "Released" && x.SettledAt >= monthStart).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var filtered = ApplyStatus(mine, status);
        var total = await filtered.CountAsync(ct);
        var rows = await ToRowsAsync(filtered.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new(nextAmount, nextDate is { } d && d < Settlements.NoReleaseDate ? d : null, onHold, paid, new PagedResponse<SellerPayoutRow>(rows, total, page, pageSize));
    }

    public async Task<string> CsvAsync(long? sellerId, string? status, CancellationToken ct)
    {
        var rows = await ToRowsAsync(ApplyStatus(db.Settlements.AsNoTracking().Where(x => sellerId == null || x.ManufacturerBusinessId == sellerId), status).OrderByDescending(x => x.Id).Take(5000), ct);
        var csv = new StringBuilder("orderNumber,orderValue,platformFee,youGet,status,releaseDate,paidAt\n");
        foreach (var r in rows)
            csv.Append(string.Join(',', r.OrderNumber, Money(r.OrderValue), Money(r.PlatformFee), Money(r.Amount), r.Status,
                r.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, r.PaidAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty)).Append('\n');
        return csv.ToString();
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private IQueryable<Settlement> ApplyStatus(IQueryable<Settlement> query, string? status) => status switch
    {
        "paid" => query.Where(x => x.Status == "Released"),
        "scheduled" => query.Where(x => x.Status == "Pending" && x.ReleaseAfter < Settlements.NoReleaseDate && !db.ReturnClaims.Any(c => c.SellerOrderId == x.SellerOrderId && c.ResolvedAtUtc == null)),
        "cancelled" => query.Where(x => x.Status == "Cancelled"),
        "on_hold" => query.Where(x => x.Status == "Pending" && (x.ReleaseAfter >= Settlements.NoReleaseDate || db.ReturnClaims.Any(c => c.SellerOrderId == x.SellerOrderId && c.ResolvedAtUtc == null))),
        _ => query
    };

    private async Task<List<SellerPayoutRow>> ToRowsAsync(IQueryable<Settlement> query, CancellationToken ct)
    {
        var settlements = await query.ToListAsync(ct);
        var ids = settlements.Select(x => x.SellerOrderId).ToArray();
        var orders = await db.SellerOrders.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Order!.OrderNumber, x.Total }).ToDictionaryAsync(x => x.Id, ct);
        var claims = (await db.ReturnClaims.AsNoTracking().Where(c => c.ResolvedAtUtc == null && ids.Contains(c.SellerOrderId)).Select(c => c.SellerOrderId).ToListAsync(ct)).ToHashSet();
        var sellerIds = settlements.Select(x => x.ManufacturerBusinessId).Distinct().ToArray();
        var sellerNames = await db.Businesses.AsNoTracking().Where(b => sellerIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.DisplayName, ct);
        return settlements.Select(x =>
        {
            var hasDate = Settlements.HasReleaseDate(x.ReleaseAfter);
            var status = x.Status switch
            {
                "Released" => "Paid",
                "Cancelled" => "Cancelled",
                _ => hasDate && !claims.Contains(x.SellerOrderId) ? "Scheduled" : "OnHold"
            };
            orders.TryGetValue(x.SellerOrderId, out var order);
            return new SellerPayoutRow(x.SellerOrderId, order?.OrderNumber ?? string.Empty, order?.Total ?? 0m, x.CommissionAmount, x.Amount, status,
                x.Status == "Pending" && hasDate ? x.ReleaseAfter : null, x.SettledAt) { SellerName = sellerNames.GetValueOrDefault(x.ManufacturerBusinessId) ?? string.Empty };
        }).ToList();
    }
}
