namespace TextileConnect.Domain.Catalogue;

public sealed class PlatformSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
