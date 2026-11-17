namespace TextileConnect.Domain.Payments;

public sealed class Invoice
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public required string InvoiceNumber { get; set; }
    /// <summary>SellerToRetailer (tax invoice), PlatformCommission (platform fee, to the seller) or CreditNote.</summary>
    public string InvoiceType { get; set; } = "SellerToRetailer";
    /// <summary>For a credit note: the refund it was made for. 0 for every other document, so that (seller order, type, refund) can be unique.</summary>
    public long RefundId { get; set; }
    public string? FileKey { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
