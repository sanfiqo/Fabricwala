using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Notifications;
using TextileConnect.Domain.Orders;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class ReturnClaimEndpoints
{
    public const int MaxPhotos = 3;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] PhotoTypes = ["image/jpeg", "image/png", "image/webp"];

    public static void MapReturnClaimEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/seller-orders").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Retailer"));
        retailer.MapPost("/{sellerOrderId:long}/claims", async (long sellerOrderId, ClaimRequest request, ICurrentBusiness current, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var sellerOrder = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.Order!.RetailerBusinessId == current.RequireBusinessId(), ct);
            if (sellerOrder is null) return Results.NotFound();
            var note = request.Note?.Trim() ?? string.Empty;
            var ordered = sellerOrder.Items.Sum(x => x.Quantity);
            if (!Enum.IsDefined(request.Reason) || request.Pieces < 1 || request.Pieces > ordered || note.Length > 1000 || (request.Reason == ClaimReason.Other && note.Length == 0))
                return Results.BadRequest();
            if (sellerOrder.Status is not ("Dispatched" or "Delivered"))
                throw new RuleViolationException("claim_not_allowed", "A problem can be reported once the order is shipped.");
            if (await db.ReturnClaims.AnyAsync(x => x.SellerOrderId == sellerOrderId && x.ResolvedAtUtc == null, ct))
                throw new RuleViolationException("claim_already_open", "A problem report is already open for this order.");
            var now = clock.GetUtcNow().UtcDateTime;
            var claim = new ReturnClaim { SellerOrderId = sellerOrderId, ReasonCode = request.Reason, Pieces = request.Pieces, Reason = note, CreatedAtUtc = now };
            db.ReturnClaims.Add(claim);
            var sellerUsers = await db.BusinessUsers.AsNoTracking().Where(x => x.BusinessId == sellerOrder.ManufacturerBusinessId).Select(x => x.UserId).ToListAsync(ct);
            db.Notifications.AddRange(sellerUsers.Select(u => new Notification { UserId = u, Title = "Problem reported", Message = $"The retailer reported a problem with order {sellerOrder.Order!.OrderNumber}.", Link = "/seller/orders", CreatedAtUtc = now }));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/seller-orders/{sellerOrderId}/claims", ClaimResponse.From(claim));
        });
        retailer.MapGet("/{sellerOrderId:long}/claims", async (long sellerOrderId, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var claims = await db.ReturnClaims.AsNoTracking().Include(x => x.Photos)
                .Where(x => x.SellerOrderId == sellerOrderId && x.SellerOrder!.Order!.RetailerBusinessId == current.RequireBusinessId())
                .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
            return Results.Ok(claims.Select(ClaimResponse.From).ToList());
        });
        retailer.MapPost("/{sellerOrderId:long}/claims/{claimId:long}/photos", async (long sellerOrderId, long claimId, IFormFile file, ICurrentBusiness current, MarketplaceDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.Photos)
                .SingleOrDefaultAsync(x => x.Id == claimId && x.SellerOrderId == sellerOrderId && x.SellerOrder!.Order!.RetailerBusinessId == current.RequireBusinessId(), ct);
            if (claim is null) return Results.NotFound();
            if (file.Length is 0 or > MaxPhotoBytes || !PhotoTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest();
            if (claim.ResolvedAtUtc is not null) throw new RuleViolationException("claim_closed", "This problem report is already resolved.");
            if (claim.Photos.Count >= MaxPhotos) throw new RuleViolationException("claim_photo_limit", "You can add up to 3 photos.");
            await using var stream = file.OpenReadStream();
            claim.Photos.Add(new ReturnClaimPhoto { FileKey = await storage.SavePrivateAsync(stream, file.FileName, ct) });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { claim.Id, photoCount = claim.Photos.Count });
        });

        var seller = app.MapGroup("/api/v1/seller/claims").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Manufacturer"));
        seller.MapPost("/{claimId:long}/respond", async (long claimId, SellerResponse request, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.Photos).Include(x => x.SellerOrder)
                .SingleOrDefaultAsync(x => x.Id == claimId && x.SellerOrder!.ManufacturerBusinessId == current.RequireBusinessId(), ct);
            if (claim is null) return Results.NotFound();
            var text = request.Response?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > 1000) return Results.BadRequest();
            if (claim.Status != "Submitted") throw new RuleViolationException("claim_not_open", "This problem report cannot be answered now.");
            claim.SellerResponse = text; claim.Status = "SellerResponded";
            await db.SaveChangesAsync(ct);
            return Results.Ok(ClaimResponse.From(claim));
        });

        var admin = app.MapGroup("/api/v1/admin/claims").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapPost("/{claimId:long}/decide", async (long claimId, AdminDecision request, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.Photos).Include(x => x.SellerOrder).ThenInclude(x => x!.Order).SingleOrDefaultAsync(x => x.Id == claimId, ct);
            if (claim is null) return Results.NotFound();
            var sellerOrder = claim.SellerOrder!;
            var refund = request.Decision == "Rejected" ? 0m : request.RefundAmount;
            if (request.Decision is not ("Approved" or "Rejected" or "PartiallyRefunded") || refund < 0 || refund > sellerOrder.Total || decimal.Round(refund, 2) != refund
                || (request.Decision != "Rejected" && refund == 0) || string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 1000) return Results.BadRequest();
            if (claim.ResolvedAtUtc is not null) throw new RuleViolationException("claim_not_open", "This problem report is already resolved.");
            var now = clock.GetUtcNow().UtcDateTime;
            claim.Status = request.Decision; claim.AdminDecision = request.Note.Trim(); claim.ApprovedRefundAmount = refund; claim.ResolvedAtUtc = now;
            if (refund > 0)
            {
                var payment = await db.Payments.Where(x => x.OrderId == sellerOrder.OrderId && x.Status == "Paid").OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
                if (payment is not null) db.Refunds.Add(new Refund { PaymentId = payment.Id, SellerOrderId = claim.SellerOrderId, Amount = refund, Reason = $"Claim {claim.Id}", CreatedAtUtc = now });
                // The refund comes out of the held seller payout; a payout that was already released is not clawed back here.
                var held = await db.Settlements.SingleOrDefaultAsync(x => x.SellerOrderId == claim.SellerOrderId && x.Status == "Pending", ct);
                if (held is not null) held.Amount = Math.Max(0m, held.Amount - refund);
            }
            var users = await db.BusinessUsers.AsNoTracking().Where(x => x.BusinessId == sellerOrder.ManufacturerBusinessId || x.BusinessId == sellerOrder.Order!.RetailerBusinessId).Select(x => x.UserId).ToListAsync(ct);
            db.Notifications.AddRange(users.Select(u => new Notification { UserId = u, Title = "Problem report decided", Message = $"Order {sellerOrder.Order!.OrderNumber}: {request.Decision}.", Link = "/orders", CreatedAtUtc = now }));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ClaimResponse.From(claim));
        });
    }

    /// <summary>Reason is one of Damaged, WrongItem, ShortQuantity, QualityIssue, Other (Other needs a note). Pieces is 1 up to the quantity ordered.</summary>
    public sealed record ClaimRequest(ClaimReason Reason, int Pieces, string? Note);
    public sealed record SellerResponse(string Response);
    public sealed record AdminDecision(string Decision, decimal RefundAmount, string Note);
}
