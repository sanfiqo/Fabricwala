using TextileConnect.Application.Catalogue;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

/// <summary>Photos for one product, for the Add/Edit product screen. The first photo (lowest sort order) is the main image.</summary>
public static class SellerProductMediaEndpoints
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxPhotosPerProduct = 8;

    public static void MapSellerProductMediaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/seller/products/{id:long}/media").RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Manufacturer"));

        // multipart/form-data with one field "file" (JPEG, PNG or WebP, up to 5 MB). The type is read from the bytes of the file, not from its name or content type.
        group.MapPost("", async (long id, IFormFile file, ICurrentBusiness current, MarketplaceDbContext db, IFileStorage storage, IImageVariantMaker maker, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.SellerBusinessId == current.RequireBusinessId() && !x.IsDeleted, ct);
            if (product is null) return Results.NotFound();
            if (file.Length == 0) throw new RuleViolationException("photo_empty", "Choose a photo to upload.", 400);
            if (file.Length > MaxBytes) throw new RuleViolationException("photo_too_large", "The photo must be 5 MB or smaller.", 413);

            var header = new byte[12];
            var read = 0;
            await using (var probe = file.OpenReadStream())
                while (read < header.Length)
                {
                    var count = await probe.ReadAsync(header.AsMemory(read), ct);
                    if (count == 0) break;
                    read += count;
                }
            var (contentType, extension) = Detect(header, read);
            if (contentType is null) throw new RuleViolationException("photo_invalid_type", "Upload a JPEG, PNG or WebP photo.", 400);

            var existing = await db.ProductMedia.Where(x => x.ProductId == id).Select(x => x.SortOrder).ToListAsync(ct);
            if (existing.Count >= MaxPhotosPerProduct) throw new RuleViolationException("photo_limit_reached", $"A product can have at most {MaxPhotosPerProduct} photos.", 409);

            // The first bytes only prove the file starts like a photo; making the copies proves it can really be read (and is not absurdly large).
            ImageVariants? variants;
            await using (var decode = file.OpenReadStream()) variants = maker.Create(decode);
            if (variants is null) throw new RuleViolationException("photo_unreadable", "This photo could not be read. Try another JPEG, PNG or WebP photo.", 400);

            async Task<string> SaveAsync(byte[] bytes, string name) { await using var stream = new MemoryStream(bytes); return await storage.SavePrivateAsync(stream, name, ct); }
            string key;
            await using (var stream = file.OpenReadStream()) key = await storage.SavePrivateAsync(stream, "photo" + extension, ct);
            var media = new ProductMedia
            {
                ProductId = id, FileKey = key, MediaType = contentType, SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1,
                MainKey = await SaveAsync(variants.Main, "main.webp"), CardKey = await SaveAsync(variants.Card, "card.webp"), ThumbKey = await SaveAsync(variants.Thumb, "thumb.webp"), Lqip = variants.Lqip
            };
            db.ProductMedia.Add(media);
            product.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/seller/products/{id}/media/{media.Id}",
                new SellerProductMediaResponse(media.Id, ProductMediaLinks.Url(product.Slug, media, ProductMediaLinks.Main), media.MediaType, media.SortOrder, existing.Count == 0)
                { SrcSet = ProductMediaLinks.SrcSet(product.Slug, media), Lqip = ProductMediaLinks.Lqip(media) });
        });

        group.MapDelete("/{mediaId:long}", async (long id, long mediaId, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.SellerBusinessId == current.RequireBusinessId() && !x.IsDeleted, ct);
            var media = product is null ? null : await db.ProductMedia.SingleOrDefaultAsync(x => x.Id == mediaId && x.ProductId == id, ct);
            if (product is null || media is null) return Results.NotFound();
            db.ProductMedia.Remove(media);
            product.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static (string? ContentType, string Extension) Detect(byte[] b, int length)
    {
        if (length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ("image/png", ".png");
        if (length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("image/webp", ".webp");
        return (null, string.Empty);
    }
}

/// <summary>One product photo. The url also works for the seller while the product is still a draft; shoppers can open it once the product is live.</summary>
public sealed record SellerProductMediaResponse(long Id, string Url, string MediaType, int SortOrder, bool IsMain)
{
    /// <summary>"... 400w, ... 800w" for the browser, and the tiny blurred preview as a data URL.</summary>
    public string? SrcSet { get; init; }
    public string? Lqip { get; init; }
}
