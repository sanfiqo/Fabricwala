namespace TextileConnect.Domain.Payments;

/// <summary>
/// The last invoice number a seller has used in one financial year. Tax invoices must be numbered in one running series per seller and year,
/// so the number is the next value of this row, taken in the same save as the invoice. LastNumber is a concurrency check: two requests that
/// take the same number cannot both succeed.
/// </summary>
public sealed class InvoiceSequence
{
    public long Id { get; set; }
    public long SellerBusinessId { get; set; }
    /// <summary>Financial year as four digits, April 2026 to March 2027 is "2627".</summary>
    public required string FinancialYear { get; set; }
    public int LastNumber { get; set; }
}
