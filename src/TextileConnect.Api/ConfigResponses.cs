using TextileConnect.Domain.Businesses;
using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Api;

// Responses and requests for the settings screens and the address book. They carry exactly the fields a screen needs,
// so a new column on a table never appears in the API by accident and a caller can never set a column it should not (NoEntityInResponsesTests).

/// <summary>Freight between two cities. It has no id: it is found by the two cities and the rate type.</summary>
public sealed record ShippingRateResponse(long FromCityId, long ToCityId, ShippingRateType RateType, decimal Rate, decimal MinCharge, int TransitDays, bool IsActive)
{
    public static ShippingRateResponse From(ShippingRate x) => new(x.FromCityId, x.ToCityId, x.RateType, x.Rate, x.MinCharge, x.TransitDays, x.IsActive);
}

/// <summary>A category as the admin sees it (the shop-facing CategoryResponse has no IsActive).</summary>
public sealed record AdminCategoryResponse(long Id, long? ParentId, string Name, string Slug, int SortOrder, bool IsActive)
{
    public static AdminCategoryResponse From(Category x) => new(x.Id, x.ParentId, x.Name, x.Slug, x.SortOrder, x.IsActive);
}

/// <summary>Name up to 120 characters; Slug in lower case letters, digits and single dashes, up to 240 characters.</summary>
public sealed record CategoryRequest(long? ParentId, string? Name, string? Slug, int SortOrder = 0, bool IsActive = true)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= 120
        && !string.IsNullOrWhiteSpace(Slug) && Slug.Length <= 240 && System.Text.RegularExpressions.Regex.IsMatch(Slug, "^[a-z0-9]+(-[a-z0-9]+)*$") && ParentId is not <= 0;
}

public sealed record BannerResponse(long Id, string ImageUrl, string? Link, DateTime StartsAtUtc, DateTime EndsAtUtc, int SortOrder, bool IsActive)
{
    public static BannerResponse From(HomeBanner x) => new(x.Id, x.ImageUrl, x.Link, x.StartsAtUtc, x.EndsAtUtc, x.SortOrder, x.IsActive);
}

/// <summary>ImageUrl must be a web address (http or https). Link is empty, a path inside the app that starts with "/", or a web address. The banner ends after it starts.</summary>
public sealed record BannerRequest(string? ImageUrl, string? Link, DateTime StartsAtUtc, DateTime EndsAtUtc, int SortOrder = 0, bool IsActive = true)
{
    private static bool IsWebAddress(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
    public bool IsValid => ImageUrl is { Length: > 0 and <= 1000 } && IsWebAddress(ImageUrl)
        && (string.IsNullOrEmpty(Link) || Link.Length <= 1000 && (Link.StartsWith('/') && !Link.StartsWith("//", StringComparison.Ordinal) || IsWebAddress(Link)))
        && EndsAtUtc > StartsAtUtc;
}

public sealed record BusinessAddressResponse(long Id, long BusinessId, AddressType AddressType, string Line1, string? Line2, long CityId, string Pincode, bool IsDefault)
{
    public static BusinessAddressResponse From(BusinessAddress x) => new(x.Id, x.BusinessId, x.AddressType, x.Line1, x.Line2, x.CityId, x.Pincode, x.IsDefault);
}

/// <summary>The business and the id of the address are never taken from the caller.</summary>
public sealed record BusinessAddressRequest(AddressType AddressType, string? Line1, string? Line2, long CityId, string? Pincode, bool IsDefault = false)
{
    public BusinessAddress ToEntity() => new() { AddressType = AddressType, Line1 = Line1!.Trim(), Line2 = string.IsNullOrWhiteSpace(Line2) ? null : Line2.Trim(), CityId = CityId, Pincode = Pincode!, IsDefault = IsDefault };
}

public sealed record StaffMemberResponse(long BusinessId, string UserId, BusinessMemberRole Role, bool IsPrimary)
{
    public static StaffMemberResponse From(BusinessUser x) => new(x.BusinessId, x.UserId, x.Role, x.IsPrimary);
}
