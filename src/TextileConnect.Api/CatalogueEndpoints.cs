using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class CatalogueEndpoints
{
    public static void MapCatalogueEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/categories", async (int? page, int? pageSize, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var current = page ?? 1; var size = pageSize ?? 100;
            if (current is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            var query = db.Categories.AsNoTracking().Where(x => x.IsActive);
            return Results.Ok(new PagedResponse<CategoryResponse>(await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Skip((current - 1) * size).Take(size).Select(x => new CategoryResponse(x.Id, x.ParentId, x.Name, x.Slug, x.SortOrder)).ToListAsync(ct),
                await query.CountAsync(ct), current, size));
        });
        app.MapGet("/api/v1/products", async ([AsParameters] CatalogueQuery query, MarketplaceDbContext db, CancellationToken ct)
            => !query.IsValid ? Results.BadRequest() : Results.Ok(await new CatalogueReader(db).ListAsync(query, ct))).RequireRateLimiting("search");
        app.MapGet("/api/v1/home", async (MarketplaceDbContext db, CancellationToken ct) =>
        {
            var categories = await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(100)
                .Select(x => new CategoryResponse(x.Id, x.ParentId, x.Name, x.Slug, x.SortOrder)).ToListAsync(ct);
            return Results.Ok(new HomeResponse(categories, await new CatalogueReader(db).ListAsync(new(Sort: "popular", PageSize: 12), ct)));
        }).RequireRateLimiting("search");
        app.MapGet("/api/v1/products/{slug}", async (string slug, MarketplaceDbContext db, CancellationToken ct)
            => await new CatalogueReader(db).DetailAsync(slug, ct) is { } product ? Results.Ok(product) : Results.NotFound());
        app.MapGet("/api/v1/products/{slug}/media/{mediaId:long}", async (string slug, long mediaId, MarketplaceDbContext db, IFileStorage storage, HttpContext context, CancellationToken ct) =>
        {
            var media = await (from product in new CatalogueReader(db).PublicProducts
                               join item in db.ProductMedia.AsNoTracking() on product.Id equals item.ProductId
                               where product.Slug == slug && item.Id == mediaId select item).SingleOrDefaultAsync(ct);
            if (media is null) return Results.NotFound();
            if (Uri.TryCreate(media.FileKey, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return Results.Redirect(uri.AbsoluteUri);
            new FileExtensionContentTypeProvider().TryGetContentType(media.FileKey, out var contentType);
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            try
            {
                var stream = await storage.OpenPrivateAsync(media.FileKey, ct);
                return contentType is "image/jpeg" or "image/png" or "image/webp" or "image/avif" or "image/gif" or "video/mp4"
                    ? Results.File(stream, contentType)
                    : Results.File(stream, "application/octet-stream", "product-media");
            }
            catch (FileNotFoundException) { return Results.NotFound(); }
        });
    }
}
