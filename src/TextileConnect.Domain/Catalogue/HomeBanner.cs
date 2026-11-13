namespace TextileConnect.Domain.Catalogue;

public sealed class HomeBanner
{
    public long Id { get; set; }
    public required string ImageUrl { get; set; }
    public string? Link { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
