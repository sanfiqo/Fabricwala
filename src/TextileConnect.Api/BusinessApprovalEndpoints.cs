using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sanfiqo.Identity;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class BusinessApprovalEndpoints
{
    public static void MapBusinessApprovalEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/businesses").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapGet("", async (string? status, int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var current = page ?? 1; var size = pageSize ?? 20;
            if (current < 1 || current > 1_000_000 || size is < 1 or > 100
                || !Enum.TryParse<VerificationStatus>(status ?? "Pending", true, out var filter) || !Enum.IsDefined(filter)) return Results.BadRequest();
            var query = db.Businesses.AsNoTracking().Where(x => !x.IsDeleted && x.VerificationStatus == filter);
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((current - 1) * size).Take(size).ToListAsync(ct);
            return Results.Ok(new PagedResponse<AdminBusinessSummaryResponse>(rows.Select(x => new AdminBusinessSummaryResponse(
                x.Id, x.BusinessType, x.DisplayName, x.LegalName, x.VerificationStatus, x.CreatedAtUtc, x.RejectionNote, MaskPhone(x.Phone))).ToList(), total, current, size));
        });
        admin.MapGet("/{id:long}", async (long id, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var business = await db.Businesses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (business is null) return Results.NotFound();
            var documents = await db.BusinessDocuments.AsNoTracking().Where(x => x.BusinessId == id).OrderBy(x => x.Id)
                .Select(x => new BusinessDocumentResponse(x.Id, x.DocumentType, x.Status, x.ReviewNote, $"/api/v1/admin/businesses/{id}/documents/{x.Id}")).ToListAsync(ct);
            var provided = !string.IsNullOrEmpty(business.Gstin);
            return Results.Ok(new BusinessReviewResponse(BusinessEndpoints.Profile(business, db) with { Phone = MaskPhone(business.Phone) },
                new GstCheckResponse(provided, provided ? BusinessInputRules.IsGstin(business.Gstin) : null, provided ? "not_checked" : "not_provided"), documents));
        });
        admin.MapGet("/{id:long}/documents/{documentId:long}", async (long id, long documentId, MarketplaceDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            if (!await db.Businesses.AnyAsync(x => x.Id == id && !x.IsDeleted, ct)) return Results.NotFound();
            var document = await db.BusinessDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.BusinessId == id, ct);
            if (document is null) return Results.NotFound();
            return Results.File(await storage.OpenPrivateAsync(document.FileKey, ct), "application/octet-stream", $"business-{id}-document-{documentId}");
        });
        admin.MapPost("/{id:long}/approve", (long id, ClaimsPrincipal principal, IBusinessDirectory directory,
            MarketplaceDbContext db, ApplicationIdentityDbContext identityDb, UserManager<ApplicationUser> users, CancellationToken ct)
            => DecideAsync(id, VerificationStatus.Approved, null, principal, directory, db, identityDb, users, ct));
        admin.MapPost("/{id:long}/reject", (long id, BusinessEndpoints.Rejection request, ClaimsPrincipal principal, IBusinessDirectory directory,
            MarketplaceDbContext db, ApplicationIdentityDbContext identityDb, UserManager<ApplicationUser> users, CancellationToken ct)
            => DecideAsync(id, VerificationStatus.Rejected, request.Note, principal, directory, db, identityDb, users, ct));
    }

    private static async Task<IResult> DecideAsync(long id, VerificationStatus status, string? note, ClaimsPrincipal principal,
        IBusinessDirectory directory, MarketplaceDbContext db, ApplicationIdentityDbContext identityDb, UserManager<ApplicationUser> users, CancellationToken ct)
    {
        BusinessVerificationRules.Validate(status, note);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction is not null)
        {
            identityDb.Database.SetDbConnection(db.Database.GetDbConnection());
            await identityDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        }
        var business = transaction is null
            ? await db.Businesses.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            : await db.Businesses.FromSqlInterpolated($"SELECT * FROM `Businesses` WHERE `Id` = {id} AND `IsDeleted` = 0 FOR UPDATE").SingleOrDefaultAsync(ct);
        if (business is null) return Results.NotFound();
        if (business.BusinessType is not (BusinessType.Retailer or BusinessType.Manufacturer)) return Results.BadRequest();
        var userIds = await db.BusinessUsers.Where(x => x.BusinessId == id).Select(x => x.UserId).ToListAsync(ct);
        var members = await users.Users.Where(x => userIds.Contains(x.Id)).ToListAsync(ct);
        if (members.Count != userIds.Count) throw new InvalidOperationException("A business member account is unavailable.");
        business = directory.SetVerification(id, status, note, principal.FindFirstValue(ClaimTypes.NameIdentifier))!;
        foreach (var user in members)
        {
            if (status == VerificationStatus.Approved)
            {
                var targetRole = business.BusinessType.ToString();
                var roles = await users.GetRolesAsync(user);
                foreach (var role in roles.Where(x => x is "Retailer" or "Manufacturer" && x != targetRole)) EnsureSuccess(await users.RemoveFromRoleAsync(user, role));
                if (!roles.Contains(targetRole)) EnsureSuccess(await users.AddToRoleAsync(user, targetRole));
            }
            var oldClaims = (await users.GetClaimsAsync(user)).Where(x => BusinessClaimTypes.All.Contains(x.Type)).ToArray();
            if (oldClaims.Length > 0) EnsureSuccess(await users.RemoveClaimsAsync(user, oldClaims));
            var claims = new List<Claim> { new(BusinessClaimTypes.BusinessId, id.ToString()), new(BusinessClaimTypes.BusinessType, business.BusinessType.ToString()) };
            if (status == VerificationStatus.Approved) claims.Add(new(BusinessClaimTypes.Approved, "true"));
            EnsureSuccess(await users.AddClaimsAsync(user, claims));
            EnsureSuccess(await users.UpdateSecurityStampAsync(user));
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(BusinessEndpoints.Profile(business, db) with { Phone = MaskPhone(business.Phone) });
    }

    private static string MaskPhone(string phone) => phone.Length > 4 ? "***" + phone[^4..] : "***";
    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Business member permissions could not be updated.");
    }
}
