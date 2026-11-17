using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;

namespace TextileConnect.Infrastructure.Persistence;

/// <summary>Finds the city of a pincode in the postal directory. Used wherever an address is saved, so a mistyped or unknown pincode gets the same clear answer everywhere.</summary>
public static class PostalDirectory
{
    public static RuleViolationException Invalid() => new("pincode_invalid", "Enter a six-digit pincode.", 400);

    public static RuleViolationException Unknown(string pincode) => new("pincode_unknown", $"We could not find pincode {pincode}. Check the six digits.", 400);

    /// <summary>The city id for the pincode. Throws pincode_invalid (not six digits) or pincode_unknown (not in the directory), both 400.</summary>
    public static async Task<long> RequireCityIdAsync(MarketplaceDbContext db, string? pincode, CancellationToken cancellationToken = default)
    {
        if (!BusinessInputRules.IsPincode(pincode)) throw Invalid();
        return await db.PostalCodes.AsNoTracking().Where(x => x.Pincode == pincode).Select(x => (long?)x.CityId).FirstOrDefaultAsync(cancellationToken)
            ?? throw Unknown(pincode!);
    }
}
