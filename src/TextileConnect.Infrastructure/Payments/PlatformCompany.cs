using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

/// <summary>
/// The company that runs the marketplace, as the admin entered it in the platform settings. It is the issuer of the platform fee invoice, so the fee
/// carries GST only while this is complete and the GSTIN is valid: charging a tax that cannot be invoiced would be worse than not charging it yet.
/// </summary>
public sealed record PlatformCompany(string LegalName, string Gstin, string Address, string Pincode, decimal FeeGstRatePercent)
{
    public const string LegalNameKey = "Platform.LegalName";
    public const string GstinKey = "Platform.Gstin";
    public const string AddressKey = "Platform.Address";
    public const string PincodeKey = "Platform.Pincode";
    public const string FeeGstRateKey = "Platform.FeeGstRate";
    public const decimal DefaultFeeGstRatePercent = 18m;

    /// <summary>The state code (first two digits) of the platform GSTIN.</summary>
    public string StateCode => Gstin[..2];

    /// <summary>The platform company, or null when any part is missing or the GSTIN is not valid.</summary>
    public static async Task<PlatformCompany?> LoadAsync(MarketplaceDbContext db, CancellationToken cancellationToken = default)
    {
        var settings = await db.PlatformSettings.AsNoTracking().Where(x => x.Key.StartsWith("Platform.")).ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
        string Get(string key) => settings.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        var gstin = Get(GstinKey).ToUpperInvariant();
        if (Get(LegalNameKey).Length == 0 || Get(AddressKey).Length == 0 || Get(PincodeKey).Length == 0 || !GstinRules.IsValid(gstin)) return null;
        var rate = decimal.TryParse(Get(FeeGstRateKey), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 100 ? parsed : DefaultFeeGstRatePercent;
        return new PlatformCompany(Get(LegalNameKey), gstin, Get(AddressKey), Get(PincodeKey), rate);
    }
}
