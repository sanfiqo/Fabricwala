using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class BulkProductEndpoints
{
    public static void MapBulkProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/seller/products/bulk").RequireAuthorization("ApprovedBusiness");
        group.MapGet("/template", () => Results.Text(BulkProductCsv.Header + "\n", "text/csv", System.Text.Encoding.UTF8, 200));
        group.MapPost("/preview", async (IFormFile file, CancellationToken ct) => Results.Ok(await BulkProductCsv.ParseAsync(file, ct)));
        group.MapPost("/import", async (IFormFile file, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var parsed = await BulkProductCsv.ParseAsync(file, ct);
            if (parsed.Errors.Count != 0) return Results.BadRequest(parsed);
            var products = parsed.Rows.Select(row => new Product { SellerBusinessId = current.RequireBusinessId(), CategoryId = row.CategoryId, Title = row.Title, Slug = row.Slug, Description = row.Description, GstRate = row.GstRate, Moq = row.Moq, Unit = row.Unit, Status = row.Status }).ToList();
            db.Products.AddRange(products); await db.SaveChangesAsync(ct); return Results.Ok(new { imported = products.Count, products = products.Select(ApiResponseMappings.Product).ToList() });
        });
        group.MapPost("/images", async (IFormFileCollection files, ICurrentBusiness current, MarketplaceDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var variants = await db.ProductVariants.Join(db.Products, variant => variant.ProductId, product => product.Id, (variant, product) => new { variant, product }).Where(x => x.product.SellerBusinessId == current.RequireBusinessId()).ToDictionaryAsync(x => x.variant.Sku, ct);
            var uploaded = 0; var missing = new List<string>();
            foreach (var file in files)
            {
                var sku = Path.GetFileNameWithoutExtension(file.FileName);
                if (!variants.ContainsKey(sku)) { missing.Add(sku); continue; }
                await using var stream = file.OpenReadStream(); var key = await storage.SavePrivateAsync(stream, file.FileName, ct);
                db.ProductMedia.Add(new ProductMedia { ProductId = variants[sku].variant.ProductId, FileKey = key, MediaType = file.ContentType, SortOrder = 0 }); uploaded++;
            }
            await db.SaveChangesAsync(ct); return Results.Ok(new { uploaded, missing });
        });
    }
}

internal static class BulkProductCsv
{
    public const string Header = "categoryId,title,slug,description,gstRate,moq,unit,status";
    public static async Task<BulkPreview> ParseAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return new([], ["CSV file is empty"]);
        using var reader = new StreamReader(file.OpenReadStream()); var lines = new List<string>(); while (await reader.ReadLineAsync(ct) is { } line) lines.Add(line);
        if (lines.Count == 0 || !string.Equals(lines[0].Trim(), Header, StringComparison.OrdinalIgnoreCase)) return new([], ["Header must be: " + Header]);
        var rows = new List<BulkRow>(); var errors = new List<string>();
        for (var i = 1; i < lines.Count; i++)
        {
            var values = lines[i].Split(','); if (values.Length != 8) { errors.Add($"Row {i + 1}: expected 8 columns"); continue; }
            if (!long.TryParse(values[0], out var categoryId) || !decimal.TryParse(values[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var gstRate) || !int.TryParse(values[5], out var moq) || !Enum.TryParse<ProductUnit>(values[6], true, out var unit) || !Enum.TryParse<ProductStatus>(values[7], true, out var status) || string.IsNullOrWhiteSpace(values[1]) || string.IsNullOrWhiteSpace(values[2])) { errors.Add($"Row {i + 1}: invalid product values"); continue; }
            rows.Add(new BulkRow(categoryId, values[1].Trim(), values[2].Trim(), string.IsNullOrWhiteSpace(values[3]) ? null : values[3].Trim(), gstRate, moq, unit, status));
        }
        return new(rows, errors);
    }
    public sealed record BulkRow(long CategoryId, string Title, string Slug, string? Description, decimal GstRate, int Moq, ProductUnit Unit, ProductStatus Status);
    public sealed record BulkPreview(IReadOnlyList<BulkRow> Rows, IReadOnlyList<string> Errors);
}
