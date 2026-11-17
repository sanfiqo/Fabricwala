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
        admin.MapPut("/settings", async (SettingsRequest request, MarketplaceDbContext db, TextileConnect.Application.Common.IAuditTrail audit, CancellationToken ct) =>
        {
            // The commission rate and the payout delay decide how much money moves, so the new values are written down with who set them.
            audit.Record("settings.updated", "PlatformSetting", null, $"Commission.DefaultRate={request.CommissionDefaultRate}; Settlement.AutoReleaseDays={request.SettlementAutoReleaseDays}");
            await UpsertAsync(db, "Commission.DefaultRate", request.CommissionDefaultRate, ct);
            await UpsertAsync(db, "Settlement.AutoReleaseDays", request.SettlementAutoReleaseDays, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await SettingsAsync(db, ct));
        });
        admin.MapGet("/shipping-rates", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok((await db.ShippingRates.AsNoTracking().ToListAsync(ct)).Select(ShippingRateResponse.From).ToList()));
        admin.MapPost("/shipping-rates", async (ShippingRateRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            if (!await CitiesExistAsync(db, request.FromCityId, request.ToCityId, ct)) return Results.BadRequest("Both cities must exist.");
            var rate = request.ToEntity();
            db.ShippingRates.Add(rate); await db.SaveChangesAsync(ct); return Results.Ok(ShippingRateResponse.From(rate));
        });
        admin.MapPut("/shipping-rates/{fromCityId:long}/{toCityId:long}/{rateType}", async (long fromCityId, long toCityId, ShippingRateType rateType, ShippingRateRequest request, MarketplaceDbContext db, CancellationToken ct) =>
        {
            var existing = await db.ShippingRates.SingleOrDefaultAsync(x => x.FromCityId == fromCityId && x.ToCityId == toCityId && x.RateType == rateType, ct);
            if (existing is null) return Results.NotFound();
            existing.Rate = request.Rate; existing.MinCharge = request.MinCharge; existing.TransitDays = request.TransitDays; existing.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct); return Results.Ok(ShippingRateResponse.From(existing));
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
    private static Task<bool> CitiesExistAsync(MarketplaceDbContext db, long from, long to, CancellationToken ct)
        => db.Cities.CountAsync(x => x.Id == from || x.Id == to, ct).ContinueWith(t => t.Result == 2, ct);
    public sealed record SettingsRequest(string CommissionDefaultRate, string SettlementAutoReleaseDays);
    public sealed record ShippingRateRequest(long FromCityId, long ToCityId, ShippingRateType RateType, decimal Rate, decimal MinCharge, int TransitDays, bool IsActive = true)
    {
        public ShippingRate ToEntity() => new() { FromCityId = FromCityId, ToCityId = ToCityId, RateType = RateType, Rate = Rate, MinCharge = MinCharge, TransitDays = TransitDays, IsActive = IsActive };
    }
}
