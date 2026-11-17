using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Application.Catalogue;

/// <summary>The one place that builds the web addresses of a product photo, so every screen gets the same ones.</summary>
public static class ProductMediaLinks
{
    public const string Main = "main";
    public const string Card = "card";
    public const string Thumb = "thumb";
    public static readonly IReadOnlyList<string> Sizes = [Main, Card, Thumb];

    private static bool IsWebAddress(string key) => Uri.TryCreate(key, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    /// <summary>The address of the photo. A size (main, card or thumb) gives the small WebP copy when the photo has one; otherwise it is the original, so old photos keep working.</summary>
    public static string Url(string slug, ProductMedia media, string? size = null)
    {
        if (IsWebAddress(media.FileKey)) return new Uri(media.FileKey).AbsoluteUri;
        var path = $"/api/v1/products/{Uri.EscapeDataString(slug)}/media/{media.Id}";
        return size is not null && HasVariant(media, size) ? $"{path}?size={size}" : path;
    }

    /// <summary>"... 400w, ... 800w" for the card and main copies, or null when the photo has none.</summary>
    public static string? SrcSet(string slug, ProductMedia media)
        => IsWebAddress(media.FileKey) || media.CardKey is null || media.MainKey is null ? null
            : $"{Url(slug, media, Card)} 400w, {Url(slug, media, Main)} 800w";

    /// <summary>A tiny data URL to show, blurred, while the photo loads. Null for photos uploaded before copies were made.</summary>
    public static string? Lqip(ProductMedia media) => media.Lqip;

    public static bool HasVariant(ProductMedia media, string size) => KeyOf(media, size) is not null;

    /// <summary>The storage key of a copy, or null when the photo has no such copy.</summary>
    public static string? KeyOf(ProductMedia media, string size) => size switch
    {
        Main => media.MainKey,
        Card => media.CardKey,
        Thumb => media.ThumbKey,
        _ => null
    };
}
