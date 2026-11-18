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

    // The figures of the document as they were printed, kept so that the registers for the accountant can be read without making every PDF again.
    // Empty for documents issued before the register existed, and for the platform fee statement, which is not a tax invoice.
    public decimal? Taxable { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? Cgst { get; set; }
    public decimal? Sgst { get; set; }
    public decimal? Igst { get; set; }
    public decimal? Total { get; set; }
    /// <summary>The other party: the retailer on an invoice or credit note, the seller on a platform fee invoice.</summary>
    public string? CounterpartyName { get; set; }
    public string? CounterpartyGstin { get; set; }
    /// <summary>The GSTIN of the one who issued the document.</summary>
    public string? IssuerGstin { get; set; }
    public string? PlaceOfSupplyCode { get; set; }
}
