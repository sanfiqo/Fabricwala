namespace TextileConnect.Domain.Catalogue;

public sealed class ProductMedia
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public required string FileKey { get; set; }
    public required string MediaType { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Storage keys of the square WebP copies (800, 400 and 120 pixels). Null for photos uploaded before copies were made or added by web address.</summary>
    public string? MainKey { get; set; }
    public string? CardKey { get; set; }
    public string? ThumbKey { get; set; }
    /// <summary>A 20 pixel wide JPEG as a data URL, shown blurred while the photo loads.</summary>
    public string? Lqip { get; set; }
}
