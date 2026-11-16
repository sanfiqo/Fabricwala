namespace TextileConnect.Api;

public sealed record CatalogueQuery(string? Q = null, long? CategoryId = null, decimal? MinPrice = null,
    decimal? MaxPrice = null, int? MaxMoq = null, long? StateId = null, string? State = null,
    long? SellerCityId = null, string? Material = null, string? Size = null, string? Colour = null,
    string? Sort = null, int Page = 1, int PageSize = 20, string? Pincode = null, string? Season = null)
{
    public bool IsValid => Page is >= 1 and <= 1_000_000 && PageSize is >= 1 and <= 100
        && Q?.Length is not > 200 && Material?.Length is not > 100 && Season?.Length is not > 80 && Size?.Length is not > 80 && Colour?.Length is not > 80
        && State?.Length is not > 100 && CategoryId is not <= 0 && StateId is not <= 0 && SellerCityId is not <= 0
        && MinPrice is not < 0 && MaxPrice is not < 0 && !(MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
        && MaxMoq is not <= 0 && (Pincode is null || Application.Businesses.BusinessInputRules.IsPincode(Pincode))
        && (Sort is null || new[] { "popular", "price-asc", "price-desc", "moq-asc", "new", "newest" }.Contains(Sort.ToLowerInvariant()));
}
