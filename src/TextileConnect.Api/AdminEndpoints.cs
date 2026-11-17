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
        // The company that issues the platform fee invoice. GST on the fee is charged only while this is complete (see docs/INVOICING.md).
        admin.MapGet("/platform-company", async (MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await PlatformCompanyResponse.LoadAsync(db, ct)));
        admin.MapPut("/platform-company", async (PlatformCompanyRequest request, MarketplaceDbContext db, TextileConnect.Application.Common.IAuditTrail audit, CancellationToken ct) =>
        {
            var gstin = (request.Gstin ?? string.Empty).Trim().ToUpperInvariant();
            var name = (request.LegalName ?? string.Empty).Trim();
            var address = (request.Address ?? string.Empty).Trim();
            var pincode = (request.Pincode ?? string.Empty).Trim();
            if (name.Length is 0 or > 120) throw new TextileConnect.Application.Common.RuleViolationException("platform_name_invalid", "Enter the legal name of the company (up to 120 characters).", 400);
            if (!TextileConnect.Application.Businesses.GstinRules.IsValid(gstin)) throw new TextileConnect.Application.Common.RuleViolationException("platform_gstin_invalid", "This GSTIN is not valid. Check the 15 characters.", 400);
            if (address.Length is 0 or > 200) throw new TextileConnect.Application.Common.RuleViolationException("platform_address_invalid", "Enter the street address (up to 200 characters).", 400);
            if (!await db.PostalCodes.AnyAsync(x => x.Pincode == pincode, ct)) throw new TextileConnect.Application.Common.RuleViolationException("platform_pincode_unknown", "This pincode is not in the directory.", 400);
            if (request.FeeGstRate is < 0 or > 100) throw new TextileConnect.Application.Common.RuleViolationException("platform_rate_invalid", "The GST rate on the fee must be between 0 and 100.", 400);
            // Changing these changes the tax on every new order, so who did it is written down (the values themselves are not secret).
            audit.Record("platform-company.updated", "PlatformSetting", null, $"Gstin={gstin}; FeeGstRate={request.FeeGstRate}");
            await UpsertAsync(db, TextileConnect.Infrastructure.Payments.PlatformCompany.LegalNameKey, name, ct);
            await UpsertAsync(db, TextileConnect.Infrastructure.Payments.PlatformCompany.GstinKey, gstin, ct);
            await UpsertAsync(db, TextileConnect.Infrastructure.Payments.PlatformCompany.AddressKey, address, ct);
            await UpsertAsync(db, TextileConnect.Infrastructure.Payments.PlatformCompany.PincodeKey, pincode, ct);
            await UpsertAsync(db, TextileConnect.Infrastructure.Payments.PlatformCompany.FeeGstRateKey, request.FeeGstRate.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await PlatformCompanyResponse.LoadAsync(db, ct));
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
    public sealed record PlatformCompanyRequest(string LegalName, string Gstin, string Address, string Pincode, decimal FeeGstRate);

    /// <summary>What is saved for the platform company. IsComplete is true when the fee carries GST on new orders.</summary>
    public sealed record PlatformCompanyResponse(string LegalName, string Gstin, string Address, string Pincode, decimal FeeGstRate, bool IsComplete)
    {
        public static async Task<PlatformCompanyResponse> LoadAsync(MarketplaceDbContext db, CancellationToken ct)
        {
            var settings = await db.PlatformSettings.AsNoTracking().Where(x => x.Key.StartsWith("Platform.")).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
            string Get(string key) => settings.TryGetValue(key, out var value) ? value : string.Empty;
            var complete = await TextileConnect.Infrastructure.Payments.PlatformCompany.LoadAsync(db, ct);
            return new(Get(TextileConnect.Infrastructure.Payments.PlatformCompany.LegalNameKey), Get(TextileConnect.Infrastructure.Payments.PlatformCompany.GstinKey),
                Get(TextileConnect.Infrastructure.Payments.PlatformCompany.AddressKey), Get(TextileConnect.Infrastructure.Payments.PlatformCompany.PincodeKey),
                complete?.FeeGstRatePercent ?? TextileConnect.Infrastructure.Payments.PlatformCompany.DefaultFeeGstRatePercent, complete is not null);
        }
    }
    public sealed record ShippingRateRequest(long FromCityId, long ToCityId, ShippingRateType RateType, decimal Rate, decimal MinCharge, int TransitDays, bool IsActive = true)
    {
        public ShippingRate ToEntity() => new() { FromCityId = FromCityId, ToCityId = ToCityId, RateType = RateType, Rate = Rate, MinCharge = MinCharge, TransitDays = TransitDays, IsActive = IsActive };
    }
}
