using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>Fills a database with a believable catalogue: many sellers in many cities, products with sizes, colours, price tiers and a photo each.</summary>
internal static class CatalogueSeeder
{
    private static readonly string[] Kinds = ["jacket", "hoodie", "shawl", "kurti", "sweater", "cardigan", "thermal innerwear", "blanket", "scarf", "cap", "gloves", "socks", "stole", "coat"];
    private static readonly string[] Styles = ["quilted", "printed", "embroidered", "plain", "striped", "woven", "knitted", "padded", "reversible", "classic"];
    private static readonly string[] Materials = ["Wool", "Cotton", "Polyester", "Fleece", "Acrylic", "Pashmina", "Silk", "Linen"];
    private static readonly string[] Seasons = ["Winter", "Summer", "Autumn", "Spring"];
    private static readonly string[] Sizes = ["S", "M", "L", "XL", "XXL"];
    private static readonly string[] Colours = ["Black", "Red", "Blue", "Green", "Grey", "Maroon", "Navy"];

    /// <summary>Adds the sellers and the products. Ids are left to the database. Returns the number of products.</summary>
    public static async Task<int> SeedAsync(MarketplaceDbContext db, int products, int sellers = 40)
    {
        var random = new Random(20261117); // the same catalogue every run, so two runs can be compared
        var cityIds = await db.Cities.OrderBy(x => x.Id).Select(x => x.Id).Take(12).ToListAsync();
        var categoryIds = await db.Categories.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var sellerRows = Enumerable.Range(1, sellers).Select(i => new Business
        {
            BusinessType = BusinessType.Manufacturer, LegalName = $"Seed Seller {i}", DisplayName = $"Seed Seller {i}", Phone = $"+9188{i:D8}",
            VerificationStatus = VerificationStatus.Approved
        }).ToList();
        db.Businesses.AddRange(sellerRows);
        await db.SaveChangesAsync();
        db.BusinessAddresses.AddRange(sellerRows.Select((s, i) => new BusinessAddress
        {
            BusinessId = s.Id, AddressType = AddressType.Pickup, Line1 = "Factory", CityId = cityIds[i % cityIds.Count], Pincode = "141001", IsDefault = true
        }));
        await db.SaveChangesAsync();

        const int batch = 1000;
        for (var start = 0; start < products; start += batch)
        {
            var rows = new List<Product>();
            for (var i = start; i < Math.Min(products, start + batch); i++)
            {
                var kind = Kinds[random.Next(Kinds.Length)];
                var style = Styles[random.Next(Styles.Length)];
                var material = Materials[random.Next(Materials.Length)];
                rows.Add(new Product
                {
                    SellerBusinessId = sellerRows[random.Next(sellerRows.Count)].Id, CategoryId = categoryIds[random.Next(categoryIds.Count)],
                    Title = $"{char.ToUpperInvariant(style[0])}{style[1..]} {material.ToLowerInvariant()} {kind} {i}", Slug = $"seed-product-{i}",
                    Description = $"A {style} {kind} made of {material.ToLowerInvariant()} for the {Seasons[random.Next(Seasons.Length)].ToLowerInvariant()} season. Wholesale lots for shops.",
                    Material = material, Season = Seasons[random.Next(Seasons.Length)], Moq = 10 * random.Next(1, 11), GstRate = 5, Status = ProductStatus.Active,
                    UnitsSold = random.Next(0, 2000)
                });
            }
            db.Products.AddRange(rows);
            await db.SaveChangesAsync();

            foreach (var product in rows)
            {
                var price = random.Next(120, 1500);
                db.PriceSlabs.AddRange(new PriceSlab { ProductId = product.Id, MinQty = product.Moq, MaxQty = product.Moq * 4, PricePerUnit = price },
                    new PriceSlab { ProductId = product.Id, MinQty = product.Moq * 4 + 1, PricePerUnit = Math.Round(price * 0.92m, 2) });
                var colour = Colours[random.Next(Colours.Length)];
                for (var s = 0; s < 3; s++)
                    db.ProductVariants.Add(new ProductVariant { ProductId = product.Id, Sku = $"SEED-{product.Id}-{s}", Size = Sizes[(s + random.Next(3)) % Sizes.Length], Colour = colour, StockQty = random.Next(0, 500) });
                db.ProductMedia.Add(new ProductMedia { ProductId = product.Id, FileKey = $"https://example.invalid/{product.Slug}.jpg", MediaType = "image/jpeg" });
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        db.ChangeTracker.AutoDetectChangesEnabled = true;
        return products;
    }
}
