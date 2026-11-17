using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class CategoryBannerEndpoints
{
    public static void MapCategoryBannerEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/banners", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok((await db.HomeBanners.AsNoTracking().Where(x => x.IsActive && x.StartsAtUtc <= DateTime.UtcNow && x.EndsAtUtc >= DateTime.UtcNow).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct)).Select(BannerResponse.From).ToList()));
        var group = app.MapGroup("/api/v1/admin/catalogue").RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapGet("/categories", async (MarketplaceDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct)).Select(AdminCategoryResponse.From).ToList()));
        group.MapPost("/categories", async (CategoryRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!request.IsValid) return Results.BadRequest();
            var category = new Category { ParentId = request.ParentId, Name = request.Name!.Trim(), Slug = request.Slug!, SortOrder = request.SortOrder, IsActive = request.IsActive };
            db.Categories.Add(category); await db.SaveChangesAsync(ct);
            return Results.Ok(AdminCategoryResponse.From(category));
        });
        group.MapPut("/categories/{id:long}", async (long id, CategoryRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!request.IsValid) return Results.BadRequest();
            var category = await db.Categories.FindAsync([id], ct);
            if (category is null) return Results.NotFound();
            category.Name = request.Name!.Trim(); category.Slug = request.Slug!; category.ParentId = request.ParentId; category.SortOrder = request.SortOrder; category.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(AdminCategoryResponse.From(category));
        });
        group.MapPost("/banners", async (BannerRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!request.IsValid) return Results.BadRequest();
            var banner = new HomeBanner { ImageUrl = request.ImageUrl!, Link = string.IsNullOrEmpty(request.Link) ? null : request.Link, StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc, SortOrder = request.SortOrder, IsActive = request.IsActive };
            db.HomeBanners.Add(banner); await db.SaveChangesAsync(ct);
            return Results.Ok(BannerResponse.From(banner));
        });
        group.MapPut("/banners/{id:long}", async (long id, BannerRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!request.IsValid) return Results.BadRequest();
            var banner = await db.HomeBanners.FindAsync([id], ct);
            if (banner is null) return Results.NotFound();
            banner.ImageUrl = request.ImageUrl!; banner.Link = string.IsNullOrEmpty(request.Link) ? null : request.Link; banner.StartsAtUtc = request.StartsAtUtc; banner.EndsAtUtc = request.EndsAtUtc; banner.SortOrder = request.SortOrder; banner.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(BannerResponse.From(banner));
        });
    }
}
