using TextileConnect.Domain;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Orders;
using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Domain.Community;
using TextileConnect.Domain.Orders;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public sealed record SavedProductRow(long ProductId, string Slug, string Title, string SellerName, int Moq, decimal? PriceFrom, bool Available);

public sealed record RatingRequest(int Stars, string? Comment);

public sealed record RatingResponse(long OrderId, int Stars, string? Comment, DateTime UpdatedAt);

public sealed record InvoiceRow(long SellerOrderId, string OrderNumber, string CounterpartyName, DateTime Date, decimal Amount, string? InvoiceNumber, string DownloadUrl);

public sealed record PincodeCheckResponse(string Pincode, string City, string State, bool Deliverable);

public sealed record ReorderLine(string Title, string? Size, string? Colour, int Quantity, string? Reason);

/// <summary>Added lines went into the cart; skipped lines say why (not_available, out_of_stock). Cart is the updated cart.</summary>
public sealed record ReorderResponse(IReadOnlyList<ReorderLine> Added, IReadOnlyList<ReorderLine> Skipped, CartResponse Cart);

public static class RetailerExtrasEndpoints
{
    public static void MapRetailerExtrasEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1").RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Retailer"));

        // Saved products
        retailer.MapGet("/saved-products", async (int? page, int? pageSize, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20;
            if (number is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            var query = db.Favourites.AsNoTracking().Where(f => f.RetailerBusinessId == current.RequireBusinessId());
            var total = await query.CountAsync(ct);
            var saved = await query.OrderByDescending(f => f.Id).Skip((number - 1) * size).Take(size).ToListAsync(ct);
            var ids = saved.Select(f => f.ProductId).ToArray();
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && !p.IsDeleted).ToDictionaryAsync(p => p.Id, ct);
            var sellers = await db.Businesses.AsNoTracking().Where(b => products.Values.Select(p => p.SellerBusinessId).Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
            var prices = (await db.PriceSlabs.AsNoTracking().Where(s => ids.Contains(s.ProductId)).ToListAsync(ct)).GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Min(s => s.PricePerUnit));
            var rows = saved.Where(f => products.ContainsKey(f.ProductId)).Select(f =>
            {
                var p = products[f.ProductId]; var seller = sellers.GetValueOrDefault(p.SellerBusinessId);
                var available = p.Status == ProductStatus.Active && seller is { VerificationStatus: VerificationStatus.Approved, IsDeleted: false };
                return new SavedProductRow(p.Id, p.Slug, p.Title, seller?.DisplayName ?? string.Empty, p.Moq, prices.TryGetValue(p.Id, out var price) ? price : null, available);
            }).ToList();
            return Results.Ok(new PagedResponse<SavedProductRow>(rows, total, number, size));
        });
        retailer.MapPut("/saved-products/{productId:long}", async (long productId, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var businessId = current.RequireBusinessId();
            var visible = await db.Products.AsNoTracking().AnyAsync(p => p.Id == productId && !p.IsDeleted && p.Status == ProductStatus.Active
                && db.Businesses.Any(b => b.Id == p.SellerBusinessId && !b.IsDeleted && b.VerificationStatus == VerificationStatus.Approved), ct);
            if (!visible) return Results.NotFound();
            if (!await db.Favourites.AnyAsync(f => f.RetailerBusinessId == businessId && f.ProductId == productId, ct))
            { db.Favourites.Add(new Favourite { RetailerBusinessId = businessId, ProductId = productId }); await db.SaveChangesAsync(ct); }
            return Results.Ok(new { saved = true });
        });
        retailer.MapDelete("/saved-products/{productId:long}", async (long productId, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Favourites.Where(f => f.RetailerBusinessId == current.RequireBusinessId() && f.ProductId == productId).ToListAsync(ct);
            if (rows.Count > 0) { db.Favourites.RemoveRange(rows); await db.SaveChangesAsync(ct); }
            return Results.NoContent(); // removing something that is not saved is fine
        });

        // Rate an order
        retailer.MapPost("/orders/{id:long}/rating", async (long id, RatingRequest request, ICurrentBusiness current, MarketplaceDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var businessId = current.RequireBusinessId();
            if (request.Stars is < 1 or > 5 || request.Comment?.Length > 1000) return Results.BadRequest();
            var order = await db.Orders.Include(o => o.SellerOrders).SingleOrDefaultAsync(o => o.Id == id && o.RetailerBusinessId == businessId, ct);
            if (order is null) return Results.NotFound();
            if (!order.SellerOrders.Any(s => s.Status == SellerOrderStatus.Delivered)) throw new RuleViolationException("order_not_delivered", "You can rate an order after it is delivered.");
            var now = clock.GetUtcNow().UtcDateTime;
            var rating = await db.OrderRatings.SingleOrDefaultAsync(r => r.OrderId == id, ct);
            if (rating is null) db.OrderRatings.Add(rating = new OrderRating { OrderId = id, RetailerBusinessId = businessId, CreatedAtUtc = now });
            rating.Stars = request.Stars; rating.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(); rating.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new RatingResponse(id, rating.Stars, rating.Comment, rating.UpdatedAtUtc));
        });
        retailer.MapGet("/orders/{id:long}/rating", async (long id, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
            await db.OrderRatings.AsNoTracking().SingleOrDefaultAsync(r => r.OrderId == id && r.RetailerBusinessId == current.RequireBusinessId(), ct) is { } r
                ? Results.Ok(new RatingResponse(r.OrderId, r.Stars, r.Comment, r.UpdatedAtUtc)) : Results.NotFound());

        // Pincode check for a delivery address
        retailer.MapGet("/account/pincode-check", async (string? pincode, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!BusinessInputRules.IsPincode(pincode)) return Results.BadRequest();
            var place = await db.PostalCodes.AsNoTracking().Where(x => x.Pincode == pincode).Join(db.Cities, x => x.CityId, c => c.Id, (x, c) => new { c.Id, c.Name, c.StateId })
                .Join(db.States, x => x.StateId, s => s.Id, (x, s) => new { x.Id, x.Name, State = s.Name }).SingleOrDefaultAsync(ct);
            if (place is null) return Results.NotFound();
            var zones = await db.DeliveryZones.AsNoTracking().Where(z => z.IsActive).ToListAsync(ct);
            var zone = zones.Where(z => pincode!.StartsWith(z.PincodePrefix, StringComparison.Ordinal)).OrderByDescending(z => z.PincodePrefix.Length).FirstOrDefault();
            var deliverable = (zone is not null && await db.DeliveryZoneRates.AnyAsync(r => r.IsActive && r.ToZoneId == zone.Id, ct)) || await db.ShippingRates.AnyAsync(r => r.IsActive && r.ToCityId == place.Id, ct);
            return Results.Ok(new PincodeCheckResponse(pincode!, place.Name, place.State, deliverable));
        });

        // Order again
        retailer.MapPost("/orders/{id:long}/reorder", async (long id, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, ICartService cart, CancellationToken ct) =>
        {
            var businessId = current.RequireBusinessId();
            var items = await db.OrderItems.AsNoTracking().Where(i => i.SellerOrder!.Order!.Id == id && i.SellerOrder.Order.RetailerBusinessId == businessId && i.VariantId != null).OrderBy(i => i.Id).ToListAsync(ct);
            if (items.Count == 0 && !await db.Orders.AnyAsync(o => o.Id == id && o.RetailerBusinessId == businessId, ct)) return Results.NotFound();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var inCart = (await cart.ListAsync(businessId, ct)).ToDictionary(c => c.VariantId, c => c.Quantity);
            var added = new List<ReorderLine>(); var skipped = new List<ReorderLine>();
            foreach (var item in items)
            {
                ReorderLine Line(string? reason = null, int? quantity = null) => new(item.Title, item.Size, item.Colour, quantity ?? item.Quantity, reason);
                var stock = await db.ProductVariants.AsNoTracking().Where(v => v.Id == item.VariantId && v.IsActive).Select(v => (int?)v.StockQty).SingleOrDefaultAsync(ct);
                if (stock is null) { skipped.Add(Line("not_available")); continue; }
                var room = stock.Value - inCart.GetValueOrDefault(item.VariantId!.Value);
                if (room <= 0) { skipped.Add(Line("out_of_stock")); continue; }
                var quantity = Math.Min(item.Quantity, room);
                try { await cart.AddItemAsync(businessId, userId, item.VariantId.Value, quantity, ct); inCart[item.VariantId.Value] = inCart.GetValueOrDefault(item.VariantId.Value) + quantity; added.Add(Line(quantity < item.Quantity ? "reduced_to_stock" : null, quantity)); }
                catch (KeyNotFoundException) { skipped.Add(Line("not_available")); }
            }
            return Results.Ok(new ReorderResponse(added, skipped, ApiResponseMappings.Cart(await cart.GetViewAsync(businessId, ct))));
        });

        // GST invoice list for the retailer; sellers use the same route for their own orders.
        app.MapGet("/api/v1/invoices", async (int? page, int? pageSize, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1; var size = pageSize ?? 20; var businessId = current.RequireBusinessId();
            if (number is < 1 or > 1_000_000 || size is < 1 or > 100) return Results.BadRequest();
            var seller = principal.IsInRole("Manufacturer");
            var query = db.SellerOrders.AsNoTracking().Where(s => (seller ? s.ManufacturerBusinessId == businessId : s.Order!.RetailerBusinessId == businessId)
                && s.Status != SellerOrderStatus.Rejected && s.Status != SellerOrderStatus.Cancelled && (s.Order!.PaymentStatus == PaymentStatus.Paid || s.Order.PaymentStatus == PaymentStatus.PartiallyRefunded));
            var total = await query.CountAsync(ct);
            var rows = await query.OrderByDescending(s => s.PlacedAtUtc).ThenByDescending(s => s.Id).Skip((number - 1) * size).Take(size)
                .Select(s => new { s.Id, s.Order!.OrderNumber, s.PlacedAtUtc, s.Total, s.ManufacturerBusinessId, s.Order.RetailerBusinessId }).ToListAsync(ct);
            var ids = rows.Select(r => r.Id).ToArray();
            var issued = (await db.Invoices.AsNoTracking().Where(i => ids.Contains(i.SellerOrderId) && i.InvoiceType == "SellerToRetailer").ToListAsync(ct)).ToDictionary(i => i.SellerOrderId, i => i.InvoiceNumber);
            var otherIds = rows.Select(r => seller ? r.RetailerBusinessId : r.ManufacturerBusinessId).Distinct().ToArray();
            var names = await db.Businesses.AsNoTracking().Where(b => otherIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.DisplayName, ct);
            return Results.Ok(new PagedResponse<InvoiceRow>(rows.Select(r => new InvoiceRow(r.Id, r.OrderNumber, names.GetValueOrDefault(seller ? r.RetailerBusinessId : r.ManufacturerBusinessId) ?? string.Empty,
                r.PlacedAtUtc, r.Total, issued.GetValueOrDefault(r.Id), $"/api/v1/invoices/seller-orders/{r.Id}/download")).ToList(), total, number, size));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Retailer", "Manufacturer"));
    }
}
