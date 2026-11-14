using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Common;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Catalogue;
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
        app.MapGet("/api/v1/products", async ([AsParameters] CatalogueQuery query, MarketplaceDbContext db, IProductPricing pricing, ICurrentBusiness current, CancellationToken ct)
            => !query.IsValid ? Results.BadRequest() : Results.Ok(await WithQuotesAsync(await new CatalogueReader(db).ListAsync(query, ct), query.Pincode, pricing, current, ct))).RequireRateLimiting("search");
        app.MapGet("/api/v1/home", async (string? pincode, MarketplaceDbContext db, IProductPricing pricing, ICurrentBusiness current, CancellationToken ct) =>
        {
            var categories = await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Take(100)
                .Select(x => new CategoryResponse(x.Id, x.ParentId, x.Name, x.Slug, x.SortOrder)).ToListAsync(ct);
            var products = await new CatalogueReader(db).ListAsync(new(Sort: "popular", PageSize: 12), ct);
            return Results.Ok(new HomeResponse(categories, await WithQuotesAsync(products, pincode, pricing, current, ct)));
        }).RequireRateLimiting("search");
        app.MapGet("/api/v1/products/{slug}", async (string slug, int? qty, string? pincode, MarketplaceDbContext db, IProductPricing pricing, ICurrentBusiness current, CancellationToken ct) =>
        {
            var product = await new CatalogueReader(db).DetailAsync(slug, ct);
            if (product is null) return Results.NotFound();
            if (qty.HasValue && (qty < product.Moq || qty > 1_000_000)) return Results.BadRequest();
            var destination = await pricing.DestinationAsync(pincode, null, current.BusinessId, ct);
            if (destination is not null)
            {
                var quotes = await pricing.QuoteAsync([new(product.Id, qty ?? product.Moq)], destination, ct);
                product = product with { LandedCost = LandedCostResponse.From(quotes[product.Id]) };
            }
            return Results.Ok(product);
        });
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

    private static async Task<PagedResponse<ProductSummaryResponse>> WithQuotesAsync(PagedResponse<ProductSummaryResponse> page,
        string? pincode, IProductPricing pricing, ICurrentBusiness current, CancellationToken ct)
    {
        var destination = await pricing.DestinationAsync(pincode, null, current.BusinessId, ct);
        if (destination is null) return page;
        var quotes = await pricing.QuoteAsync(page.Items.Select(x => new ProductQuantity(x.Id, x.Moq)).ToArray(), destination, ct);
        return page with { Items = page.Items.Select(x => x with { LandedCost = LandedCostResponse.From(quotes[x.Id]) }).ToList() };
    }
}
