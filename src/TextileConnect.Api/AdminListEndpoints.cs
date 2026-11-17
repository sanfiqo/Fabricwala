using TextileConnect.Domain;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed record AdminOrderRow(long Id, string OrderNumber, DateTime PlacedAt, decimal GrandTotal, string PaymentStatus, string Status, string Group,
    int ItemCount, int TotalQuantity, string RetailerName, IReadOnlyList<string> SellerNames);

public sealed record AdminClaimRow(long Id, long SellerOrderId, string OrderNumber, string SellerName, string RetailerName, string Reason, int Pieces, string Status,
    int PhotoCount, DateTime CreatedAt, DateTime? ResolvedAt);

public sealed record AdminClaimPhoto(long Id, string Url);

public sealed record AdminClaimDetail(ClaimResponse Claim, string OrderNumber, string SellerName, string RetailerName, decimal OrderTotal, IReadOnlyList<AdminClaimPhoto> Photos);

/// <summary>Orders is the number of live paid orders; Value is goods value for sellers and the amount paid for retailers; Commission only applies to sellers.</summary>
public sealed record AdminPartyRow(long Id, string Name, string City, string Status, DateTime CreatedAt, int Orders, decimal Value, decimal Commission, int ActiveProducts);

public sealed record AdminProductRow(long Id, string Title, string Slug, string SellerName, string Status, bool AdminHold, int Moq, decimal? PriceFrom, DateTime UpdatedAt);

public sealed record AdminPayoutList(decimal OnHoldAmount, decimal ScheduledAmount, decimal PaidThisMonth, PagedResponse<SellerPayoutRow> Payouts);

public sealed record AuditLogRow(long Id, string? UserId, string Action, string EntityName, string? EntityId, string? IpAddress, string? Details, DateTime CreatedAt);

/// <summary>Rate is a fraction (0.03 is 3%); null means the seller pays the platform default.</summary>
public sealed record CommissionRateRequest(decimal? Rate);

public sealed record CommissionRateResponse(long SellerId, decimal? Rate, bool UsesDefault);

public sealed record AdminCommissionRow(long SellerId, string SellerName, int Orders, decimal GoodsValue, decimal Commission);

/// <summary>Commission is the platform fee on seller orders that were paid and not rejected or cancelled, by the day they were placed (UTC, From and To included).</summary>
public sealed record AdminCommissionReport(DateTime From, DateTime To, int Orders, decimal GoodsValue, decimal Commission, IReadOnlyList<AdminCommissionRow> Sellers);

public static class AdminListEndpoints
{
    public static void MapAdminListEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Admin"));

        // The audit log, newest first. Filters: action starts with, entity name, user id, and a date range (UTC, yyyy-MM-dd). Never holds bodies or secrets (docs/AUDIT.md).
        admin.MapGet("/audit-log", async (string? action, string? entity, string? userId, string? from, string? to, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (!ValidPaging(number, size) || action?.Length > 60 || entity?.Length > 60 || userId?.Length > 64) return Results.BadRequest();
            var query = db.AuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.StartsWith(action));
            if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(x => x.EntityName == entity);
            if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(x => x.UserId == userId);
            if (!string.IsNullOrWhiteSpace(from)) { if (!TryDate(from, out var start)) return Results.BadRequest(); query = query.Where(x => x.CreatedAtUtc >= start); }
            if (!string.IsNullOrWhiteSpace(to)) { if (!TryDate(to, out var end)) return Results.BadRequest(); query = query.Where(x => x.CreatedAtUtc < end.AddDays(1)); }
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((number - 1) * size).Take(size)
                .Select(x => new AuditLogRow(x.Id, x.UserId, x.Action, x.EntityName, x.EntityId, x.IpAddress, x.Details, x.CreatedAtUtc)).ToListAsync(ct);
            return Results.Ok(new PagedResponse<AuditLogRow>(rows, total, number, size));
        });

        // The commission rate of one seller. A rate between 0 and 1 (0.03 is 3%) replaces the platform default for orders placed from now on; null goes back to the default.
        // Orders already placed keep the rate they were placed with.
        admin.MapPut("/sellers/{id:long}/commission-rate", async (long id, CommissionRateRequest request, MarketplaceDbContext db, IAuditTrail audit, CancellationToken ct) =>
        {
            if (request.Rate is < 0m or > 1m) throw new RuleViolationException("commission_rate_invalid", "The rate must be between 0 and 1 (0.03 means 3%).", 400);
            var seller = await db.Businesses.SingleOrDefaultAsync(x => x.Id == id && x.BusinessType == BusinessType.Manufacturer && !x.IsDeleted, ct);
            if (seller is null) return Results.NotFound();
            var rate = request.Rate is null ? (decimal?)null : decimal.Round(request.Rate.Value, 4, MidpointRounding.AwayFromZero);
            audit.Record("seller.commission-rate", nameof(Business), seller.Id, $"{seller.CommissionRateOverride?.ToString(CultureInfo.InvariantCulture) ?? "default"} -> {rate?.ToString(CultureInfo.InvariantCulture) ?? "default"}");
            seller.CommissionRateOverride = rate;
            seller.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CommissionRateResponse(seller.Id, seller.CommissionRateOverride, seller.CommissionRateOverride is null));
        });

        admin.MapGet("/orders", async (string? status, string? q, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (!RetailerOrderReader.IsValidGroup(status) || q?.Length > 40 || !ValidPaging(number, size)) return Results.BadRequest();
            var list = await new RetailerOrderReader(db).ListAllAsync(q, status, number, size, ct);
            var ids = list.Items.Select(x => x.Id).ToArray();
            var retailers = await db.Orders.AsNoTracking().Where(o => ids.Contains(o.Id)).Join(db.Businesses, o => o.RetailerBusinessId, b => b.Id, (o, b) => new { o.Id, b.DisplayName }).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
            return Results.Ok(new PagedResponse<AdminOrderRow>(list.Items.Select(x => new AdminOrderRow(x.Id, x.OrderNumber, x.PlacedAt, x.GrandTotal, x.PaymentStatus, x.Status, x.Group,
                x.ItemCount, x.TotalQuantity, retailers.GetValueOrDefault(x.Id) ?? string.Empty, x.SellerNames)).ToList(), list.Total, list.Page, list.PageSize));
        });
        admin.MapGet("/orders/{id:long}", async (long id, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var retailerId = await db.Orders.AsNoTracking().Where(o => o.Id == id).Select(o => (long?)o.RetailerBusinessId).SingleOrDefaultAsync(ct);
            return retailerId is null ? Results.NotFound() : Results.Ok(await new RetailerOrderReader(db).DetailAsync(retailerId.Value, id, ct));
        });

        admin.MapGet("/claims", async (string? status, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (status is not (null or "all" or "open" or "resolved") || !ValidPaging(number, size)) return Results.BadRequest();
            var query = db.ReturnClaims.AsNoTracking().AsQueryable();
            if (status == "open") query = query.Where(c => c.ResolvedAtUtc == null);
            if (status == "resolved") query = query.Where(c => c.ResolvedAtUtc != null);
            var total = await query.CountAsync(ct);
            var rows = await (from c in query.OrderByDescending(c => c.ResolvedAtUtc == null).ThenByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id).Skip((number - 1) * size).Take(size)
                              join so in db.SellerOrders on c.SellerOrderId equals so.Id
                              join o in db.Orders on so.OrderId equals o.Id
                              join seller in db.Businesses on so.ManufacturerBusinessId equals seller.Id
                              join retailer in db.Businesses on o.RetailerBusinessId equals retailer.Id
                              select new { c, o.OrderNumber, Seller = seller.DisplayName, Retailer = retailer.DisplayName, Photos = c.Photos.Count }).ToListAsync(ct);
            return Results.Ok(new PagedResponse<AdminClaimRow>(rows.Select(x => new AdminClaimRow(x.c.Id, x.c.SellerOrderId, x.OrderNumber, x.Seller, x.Retailer, x.c.ReasonCode.ToString(), x.c.Pieces,
                x.c.Status, x.Photos, x.c.CreatedAtUtc, x.c.ResolvedAtUtc)).ToList(), total, number, size)); // open claims first
        });
        admin.MapGet("/claims/{id:long}", async (long id, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.AsNoTracking().Include(c => c.Photos).Include(c => c.SellerOrder).ThenInclude(s => s!.Order).SingleOrDefaultAsync(c => c.Id == id, ct);
            if (claim is null) return Results.NotFound();
            var names = await db.Businesses.AsNoTracking().Where(b => b.Id == claim.SellerOrder!.ManufacturerBusinessId || b.Id == claim.SellerOrder.Order!.RetailerBusinessId).ToDictionaryAsync(b => b.Id, b => b.DisplayName, ct);
            return Results.Ok(new AdminClaimDetail(ClaimResponse.From(claim), claim.SellerOrder!.Order!.OrderNumber, names.GetValueOrDefault(claim.SellerOrder.ManufacturerBusinessId) ?? string.Empty,
                names.GetValueOrDefault(claim.SellerOrder.Order.RetailerBusinessId) ?? string.Empty, claim.SellerOrder.Total,
                claim.Photos.OrderBy(p => p.Id).Select(p => new AdminClaimPhoto(p.Id, $"/api/v1/admin/claims/{id}/photos/{p.Id}")).ToList()));
        });
        admin.MapGet("/claims/{id:long}/photos/{photoId:long}", async (long id, long photoId, MarketplaceDbContext db, IFileStorage storage, HttpContext context, CancellationToken ct) =>
        {
            var photo = await db.ReturnClaimPhotos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId && p.ReturnClaimId == id, ct);
            if (photo is null) return Results.NotFound();
            try
            {
                await using var stream = await storage.OpenPrivateAsync(photo.FileKey, ct);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, ct);
                var bytes = memory.ToArray();
                var type = ImageType(bytes);
                if (type is null) return Results.NotFound(); // never serve something that is not a known image type
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers.CacheControl = "private, no-store";
                return Results.File(bytes, type);
            }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });

        admin.MapGet("/sellers", (string? status, string? q, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) => PartiesAsync(BusinessType.Manufacturer, status, q, page, pageSize, db, ct));
        admin.MapGet("/retailers", (string? status, string? q, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) => PartiesAsync(BusinessType.Retailer, status, q, page, pageSize, db, ct));

        admin.MapGet("/products", async (string? status, string? q, bool? held, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (!ValidPaging(number, size) || q?.Length > 100 || (status is not null && !Enum.TryParse<ProductStatus>(status, true, out _))) return Results.BadRequest();
            var query = db.Products.AsNoTracking().Where(p => !p.IsDeleted);
            if (status is not null) { var wanted = Enum.Parse<ProductStatus>(status, true); query = query.Where(p => p.Status == wanted); }
            if (held == true) query = query.Where(p => p.AdminHold);
            if (!string.IsNullOrWhiteSpace(q)) { var search = q.Trim(); query = query.Where(p => p.Title.Contains(search)); }
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(p => p.UpdatedAtUtc).ThenByDescending(p => p.Id).Skip((number - 1) * size).Take(size).ToListAsync(ct);
            var ids = rows.Select(p => p.Id).ToArray(); var sellerIds = rows.Select(p => p.SellerBusinessId).Distinct().ToArray();
            var prices = (await db.PriceSlabs.AsNoTracking().Where(s => ids.Contains(s.ProductId)).ToListAsync(ct)).GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Min(s => s.PricePerUnit));
            var sellers = await db.Businesses.AsNoTracking().Where(b => sellerIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.DisplayName, ct);
            return Results.Ok(new PagedResponse<AdminProductRow>(rows.Select(p => new AdminProductRow(p.Id, p.Title, p.Slug, sellers.GetValueOrDefault(p.SellerBusinessId) ?? string.Empty,
                p.Status.ToString(), p.AdminHold, p.Moq, prices.TryGetValue(p.Id, out var price) ? price : null, p.UpdatedAtUtc)).ToList(), total, number, size));
        });

        admin.MapGet("/payouts", async (string? status, int? page, int? pageSize, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (!SellerPayoutReader.IsValidStatus(status) || !ValidPaging(number, size)) return Results.BadRequest();
            var summary = await new SellerPayoutReader(db, clock).SummaryAsync(null, status, number, size, ct);
            return Results.Ok(new AdminPayoutList(summary.OnHoldAmount, summary.NextPayoutAmount, summary.PaidThisMonth, summary.Payouts));
        });

        admin.MapGet("/commission", async (string? from, string? to, bool? csv, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var today = clock.GetUtcNow().UtcDateTime.Date;
            var start = today.AddDays(1 - today.Day); var end = today;
            if ((from is not null && !TryDate(from, out start)) || (to is not null && !TryDate(to, out end)) || start > end || (end - start).TotalDays > 366) return Results.BadRequest();
            var until = end.AddDays(1);
            var live = db.SellerOrders.AsNoTracking().Where(s => s.PlacedAtUtc >= start && s.PlacedAtUtc < until && s.Status != SellerOrderStatus.Rejected && s.Status != SellerOrderStatus.Cancelled
                && (s.Order!.PaymentStatus == PaymentStatus.Paid || s.Order.PaymentStatus == PaymentStatus.PartiallyRefunded));
            var grouped = await live.GroupBy(s => s.ManufacturerBusinessId).Select(g => new { SellerId = g.Key, Orders = g.Count(), Goods = g.Sum(s => s.SubTotal), Fee = g.Sum(s => s.CommissionAmount) }).ToListAsync(ct);
            var ids = grouped.Select(g => g.SellerId).ToArray();
            var names = await db.Businesses.AsNoTracking().Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.DisplayName, ct);
            var rows = grouped.Select(g => new AdminCommissionRow(g.SellerId, names.GetValueOrDefault(g.SellerId) ?? string.Empty, g.Orders, g.Goods, g.Fee)).OrderByDescending(r => r.Commission).ThenBy(r => r.SellerId).ToList();
            var report = new AdminCommissionReport(start, end, rows.Sum(r => r.Orders), rows.Sum(r => r.GoodsValue), rows.Sum(r => r.Commission), rows);
            if (csv != true) return Results.Ok(report);
            var text = new StringBuilder("sellerId,seller,orders,goodsValue,commission\n");
            foreach (var r in rows) text.Append(string.Join(',', r.SellerId, CsvCell.Text(r.SellerName), r.Orders, Money(r.GoodsValue), Money(r.Commission))).Append('\n');
            return Results.File(Encoding.UTF8.GetBytes(text.ToString()), "text/csv; charset=utf-8", "commission.csv");
        });
    }

    private static async Task<IResult> PartiesAsync(BusinessType type, string? status, string? q, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct)
    {
        var number = page ?? 1; var size = pageSize ?? 20;
        if (!ValidPaging(number, size) || q?.Length > 100 || (status is not null && !Enum.TryParse<VerificationStatus>(status, true, out _))) return Results.BadRequest();
        var query = db.Businesses.AsNoTracking().Where(b => !b.IsDeleted && b.BusinessType == type);
        if (status is not null) { var wanted = Enum.Parse<VerificationStatus>(status, true); query = query.Where(b => b.VerificationStatus == wanted); }
        if (!string.IsNullOrWhiteSpace(q)) { var search = q.Trim(); query = query.Where(b => b.DisplayName.Contains(search) || b.LegalName.Contains(search)); }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(b => b.CreatedAtUtc).ThenByDescending(b => b.Id).Skip((number - 1) * size).Take(size).ToListAsync(ct);
        var ids = rows.Select(b => b.Id).ToArray();
        var cities = await db.BusinessAddresses.AsNoTracking().Where(a => ids.Contains(a.BusinessId) && a.AddressType == AddressType.Registered).Join(db.Cities, a => a.CityId, c => c.Id, (a, c) => new { a.BusinessId, c.Name }).ToListAsync(ct);
        var cityOf = cities.GroupBy(x => x.BusinessId).ToDictionary(g => g.Key, g => g.First().Name);
        Dictionary<long, (int Orders, decimal Value, decimal Fee)> stats;
        if (type == BusinessType.Manufacturer)
            stats = (await db.SellerOrders.AsNoTracking().Where(s => ids.Contains(s.ManufacturerBusinessId) && s.Status != SellerOrderStatus.Rejected && s.Status != SellerOrderStatus.Cancelled && (s.Order!.PaymentStatus == PaymentStatus.Paid || s.Order.PaymentStatus == PaymentStatus.PartiallyRefunded))
                .GroupBy(s => s.ManufacturerBusinessId).Select(g => new { g.Key, Orders = g.Count(), Value = g.Sum(s => s.SubTotal), Fee = g.Sum(s => s.CommissionAmount) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => (x.Orders, x.Value, x.Fee));
        else
            stats = (await db.Orders.AsNoTracking().Where(o => ids.Contains(o.RetailerBusinessId) && (o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.PartiallyRefunded))
                .GroupBy(o => o.RetailerBusinessId).Select(g => new { g.Key, Orders = g.Count(), Value = g.Sum(o => o.GrandTotal) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => (x.Orders, x.Value, 0m));
        var products = (await db.Products.AsNoTracking().Where(p => ids.Contains(p.SellerBusinessId) && !p.IsDeleted && p.Status == ProductStatus.Active).GroupBy(p => p.SellerBusinessId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.Count);
        return Results.Ok(new PagedResponse<AdminPartyRow>(rows.Select(b =>
        {
            stats.TryGetValue(b.Id, out var s);
            return new AdminPartyRow(b.Id, b.DisplayName, cityOf.GetValueOrDefault(b.Id) ?? string.Empty, b.VerificationStatus.ToString(), b.CreatedAtUtc, s.Orders, s.Value, s.Fee, products.GetValueOrDefault(b.Id));
        }).ToList(), total, number, size));
    }

    private static bool ValidPaging(int page, int size) => page is >= 1 and <= 1_000_000 && size is >= 1 and <= 100;

    private static bool TryDate(string value, out DateTime date)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);


    /// <summary>Recognises PNG, JPEG and WebP by their first bytes. Anything else is not served.</summary>
    internal static string? ImageType(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        return null;
    }
}
