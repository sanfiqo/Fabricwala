using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class ProductModerationEndpoints
{
    public static void MapProductModerationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/products").RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapGet("", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok((await db.Products.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Status).ThenByDescending(x => x.Id).ToListAsync(ct)).Select(ApiResponseMappings.Product).ToList()));
        group.MapPost("/{id:long}/moderate", async (long id, ModerationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (product is null || request.Action is not ("Flag" or "Deactivate" or "Activate")) return Results.BadRequest();
            product.Status = request.Action == "Activate" ? ProductStatus.Active : request.Action == "Deactivate" ? ProductStatus.Inactive : product.Status;
            db.AuditLogs.Add(new AuditLog { UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier), Action = $"Product{request.Action}", EntityName = nameof(Product), EntityId = id.ToString() });
            if (request.Action == "Flag")
            {
                var sellerUsers = await db.BusinessUsers.Where(x => x.BusinessId == product.SellerBusinessId).Select(x => x.UserId).ToListAsync(ct);
                db.Notifications.AddRange(sellerUsers.Select(userId => new Domain.Notifications.Notification { UserId = userId, Title = "Product flagged", Message = request.Reason, Link = $"/seller/products/{id}" }));
            }
            await db.SaveChangesAsync(ct); return Results.Ok(ApiResponseMappings.Product(product));
        });
    }
    public sealed record ModerationRequest(string Action, string Reason);
}
