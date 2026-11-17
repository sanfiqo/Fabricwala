using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Domain.Rfq;

/// <summary>
/// A retailer's request for a price: "I need 500 school sweaters in navy, delivered to Indore in a month". Manufacturers answer with quotations and the retailer picks one.
/// It is open for a limited time. Status is Open, Awarded, Cancelled or Closed (by an admin); an Open request whose time has passed is shown as Expired without being rewritten.
/// </summary>
public sealed class Rfq : ITimestamped
{
    public long Id { get; set; }
    public long RetailerBusinessId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public int Quantity { get; set; }
    public ProductUnit Unit { get; set; } = ProductUnit.Piece;
    /// <summary>The price the buyer hopes for. Optional, and shown to manufacturers.</summary>
    public decimal? TargetPricePerUnit { get; set; }
    public required string DeliveryPincode { get; set; }
    /// <summary>City and state of the delivery pincode when the request was made, so manufacturers see where the goods go without the address of the buyer.</summary>
    public required string DeliveryCity { get; set; }
    public required string DeliveryState { get; set; }
    public DateTime? NeededByUtc { get; set; }
    public string Status { get; set; } = RfqStatus.Open;
    public DateTime ExpiresAtUtc { get; set; }
    public long? AwardedQuotationId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A manufacturer's answer to a request. A manufacturer has one quotation per request: sending again changes it. Status is Submitted, Withdrawn, Accepted or Rejected.</summary>
public sealed class RfqQuotation : ITimestamped
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public long SellerBusinessId { get; set; }
    public decimal PricePerUnit { get; set; }
    /// <summary>How many pieces the manufacturer offers at that price (at most what was asked).</summary>
    public int Quantity { get; set; }
    public int LeadTimeDays { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = QuotationStatus.Submitted;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class RfqStatus
{
    public const string Open = "Open";
    public const string Awarded = "Awarded";
    public const string Cancelled = "Cancelled";
    public const string Closed = "Closed";
    public static readonly IReadOnlyList<string> All = [Open, Awarded, Cancelled, Closed];
}

public static class QuotationStatus
{
    public const string Submitted = "Submitted";
    public const string Withdrawn = "Withdrawn";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public static readonly IReadOnlyList<string> All = [Submitted, Withdrawn, Accepted, Rejected];
}
