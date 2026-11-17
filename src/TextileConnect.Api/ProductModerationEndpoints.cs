using TextileConnect.Application.Common;
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
        group.MapPost("/{id:long}/moderate", async (long id, ModerationRequest request, IAuditTrail audit, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (product is null || request.Action is not ("Flag" or "Deactivate" or "Activate")) return Results.BadRequest();
            product.Status = request.Action == "Activate" ? ProductStatus.Active : request.Action == "Deactivate" ? ProductStatus.Inactive : product.Status;
            if (request.Action == "Deactivate") product.AdminHold = true;
            else if (request.Action == "Activate") product.AdminHold = false;
            audit.Record($"product.{request.Action.ToLowerInvariant()}", nameof(Product), id);
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
