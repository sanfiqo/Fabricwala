using Microsoft.EntityFrameworkCore;
using TextileConnect.Domain.Catalogue;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/settings", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await SettingsAsync(db, ct)));
        admin.MapPut("/settings", async (SettingsRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            await UpsertAsync(db, "Commission.DefaultRate", request.CommissionDefaultRate, ct);
            await UpsertAsync(db, "Settlement.AutoReleaseDays", request.SettlementAutoReleaseDays, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await SettingsAsync(db, ct));
        });
        admin.MapGet("/shipping-rates", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.ShippingRates.AsNoTracking().ToListAsync(ct)));
        admin.MapPost("/shipping-rates", async (ShippingRate request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            db.ShippingRates.Add(request); await db.SaveChangesAsync(ct); return Results.Ok(request);
        });
        admin.MapPut("/shipping-rates/{fromCityId:long}/{toCityId:long}/{rateType}", async (long fromCityId, long toCityId, ShippingRateType rateType, ShippingRate request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var existing = await db.ShippingRates.SingleOrDefaultAsync(x => x.FromCityId == fromCityId && x.ToCityId == toCityId && x.RateType == rateType, ct);
            if (existing is null) return Results.NotFound();
            existing.Rate = request.Rate; existing.MinCharge = request.MinCharge; existing.TransitDays = request.TransitDays; existing.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct); return Results.Ok(existing);
        });
        admin.MapDelete("/shipping-rates/{fromCityId:long}/{toCityId:long}/{rateType}", async (long fromCityId, long toCityId, ShippingRateType rateType, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var existing = await db.ShippingRates.SingleOrDefaultAsync(x => x.FromCityId == fromCityId && x.ToCityId == toCityId && x.RateType == rateType, ct);
            if (existing is null) return Results.NotFound(); db.ShippingRates.Remove(existing); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
    }

    private static async Task<Dictionary<string, string>> SettingsAsync(MarketplaceDbContext db, CancellationToken ct)
        => await db.PlatformSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value, ct);
    private static async Task UpsertAsync(MarketplaceDbContext db, string key, string value, CancellationToken ct)
    {
        var setting = await db.PlatformSettings.SingleOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null) db.PlatformSettings.Add(new Domain.Catalogue.PlatformSetting { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow });
        else { setting.Value = value; setting.UpdatedAtUtc = DateTime.UtcNow; }
    }
    public sealed record SettingsRequest(string CommissionDefaultRate, string SettlementAutoReleaseDays);
}
