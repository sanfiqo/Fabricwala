using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class CategoryBannerEndpoints
{
    public static void MapCategoryBannerEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/banners", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.HomeBanners.AsNoTracking().Where(x => x.IsActive && x.StartsAtUtc <= DateTime.UtcNow && x.EndsAtUtc >= DateTime.UtcNow).OrderBy(x => x.SortOrder).ToListAsync(ct)));
        var group = app.MapGroup("/api/v1/admin/catalogue").RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapGet("/categories", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct)));
        group.MapPost("/categories", async (Category request, MarketplaceDbContext db, CancellationToken ct) => { db.Categories.Add(request); await db.SaveChangesAsync(ct); return Results.Ok(request); });
        group.MapPut("/categories/{id:long}", async (long id, Category request, MarketplaceDbContext db, CancellationToken ct) => { var category = await db.Categories.FindAsync([id], ct); if (category is null) return Results.NotFound(); category.Name = request.Name; category.Slug = request.Slug; category.ParentId = request.ParentId; category.SortOrder = request.SortOrder; category.IsActive = request.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(category); });
        group.MapPost("/banners", async (HomeBanner request, MarketplaceDbContext db, CancellationToken ct) => { db.HomeBanners.Add(request); await db.SaveChangesAsync(ct); return Results.Ok(request); });
        group.MapPut("/banners/{id:long}", async (long id, HomeBanner request, MarketplaceDbContext db, CancellationToken ct) => { var banner = await db.HomeBanners.FindAsync([id], ct); if (banner is null) return Results.NotFound(); banner.ImageUrl = request.ImageUrl; banner.Link = request.Link; banner.StartsAtUtc = request.StartsAtUtc; banner.EndsAtUtc = request.EndsAtUtc; banner.SortOrder = request.SortOrder; banner.IsActive = request.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(banner); });
    }
}
