namespace TextileConnect.Domain.Orders;

/// <summary>The retailer rating of a delivered order (1 to 5 stars). One per order; sending it again updates it.</summary>
public sealed class OrderRating
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long RetailerBusinessId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
