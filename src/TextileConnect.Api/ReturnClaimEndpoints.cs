using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class ReturnClaimEndpoints
{
    public static void MapReturnClaimEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/seller-orders").RequireAuthorization("ApprovedBusiness");
        retailer.MapPost("/{sellerOrderId:long}/claims", async (long sellerOrderId, ClaimRequest request, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var sellerOrder = await db.SellerOrders.Include(x => x.Order).SingleOrDefaultAsync(x => x.Id == sellerOrderId && x.Order!.RetailerBusinessId == current.RequireBusinessId() && x.Status == "Delivered", ct);
            if (sellerOrder is null || string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest();
            var claim = new ReturnClaim { SellerOrderId = sellerOrderId, Reason = request.Reason.Trim() };
            db.ReturnClaims.Add(claim); await db.SaveChangesAsync(ct); return Results.Ok(claim);
        });
        retailer.MapPost("/{sellerOrderId:long}/claims/{claimId:long}/photos", async (long sellerOrderId, long claimId, IFormFile file, ICurrentBusiness current, MarketplaceDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.Photos).Include(x => x.SellerOrder).ThenInclude(x => x.Order).SingleOrDefaultAsync(x => x.Id == claimId && x.SellerOrderId == sellerOrderId && x.SellerOrder!.Order!.RetailerBusinessId == current.RequireBusinessId(), ct);
            if (claim is null || file.Length == 0) return Results.NotFound();
            await using var stream = file.OpenReadStream(); var key = await storage.SavePrivateAsync(stream, file.FileName, ct);
            claim.Photos.Add(new ReturnClaimPhoto { FileKey = key }); await db.SaveChangesAsync(ct); return Results.Ok(new { claim.Id, photoCount = claim.Photos.Count });
        });
        var seller = app.MapGroup("/api/v1/seller/claims").RequireAuthorization("ApprovedBusiness");
        seller.MapPost("/{claimId:long}/respond", async (long claimId, SellerResponse request, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.SellerOrder).SingleOrDefaultAsync(x => x.Id == claimId && x.SellerOrder!.ManufacturerBusinessId == current.RequireBusinessId(), ct);
            if (claim is null || claim.Status != "Submitted") return Results.NotFound();
            claim.SellerResponse = request.Response; claim.Status = "SellerResponded"; await db.SaveChangesAsync(ct); return Results.Ok(claim);
        });
        var admin = app.MapGroup("/api/v1/admin/claims").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapPost("/{claimId:long}/decide", async (long claimId, AdminDecision request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var claim = await db.ReturnClaims.Include(x => x.SellerOrder).ThenInclude(x => x.Order).SingleOrDefaultAsync(x => x.Id == claimId, ct);
            if (claim is null || request.Decision is not ("Approved" or "Rejected" or "PartiallyRefunded")) return Results.BadRequest();
            claim.Status = request.Decision; claim.AdminDecision = request.Note; claim.ApprovedRefundAmount = request.Decision == "Rejected" ? 0 : request.RefundAmount; claim.ResolvedAtUtc = DateTime.UtcNow;
            if (claim.ApprovedRefundAmount > 0)
            {
                var payment = await db.Payments.Where(x => x.OrderId == claim.SellerOrder!.OrderId && x.Status == "Paid").FirstOrDefaultAsync(ct);
                if (payment is not null) db.Refunds.Add(new Domain.Payments.Refund { PaymentId = payment.Id, SellerOrderId = claim.SellerOrderId, Amount = claim.ApprovedRefundAmount.Value, Reason = $"Claim {claim.Id}: {request.Note}" });
            }
            await db.SaveChangesAsync(ct); return Results.Ok(claim);
        });
    }

    public sealed record ClaimRequest(string Reason);
    public sealed record SellerResponse(string Response);
    public sealed record AdminDecision(string Decision, decimal RefundAmount, string Note);
}
