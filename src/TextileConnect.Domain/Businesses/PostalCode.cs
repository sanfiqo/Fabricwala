namespace TextileConnect.Domain.Businesses;

public sealed class PostalCode
{
    public long Id { get; set; }
    public required string Pincode { get; set; }
    public long CityId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
