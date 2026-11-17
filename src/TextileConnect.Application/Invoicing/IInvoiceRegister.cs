using TextileConnect.Application.Common;

namespace TextileConnect.Application.Invoicing;

/// <summary>Whose point of view the line is from: a Sale is something the reader issued, a Purchase something they received (the platform fee a seller pays).</summary>
public static class RegisterDirection
{
    public const string Sale = "Sale";
    public const string Purchase = "Purchase";
}

/// <summary>
/// One document in the register. Kind is TaxInvoice, CreditNote or PlatformFeeInvoice. Amounts are positive as printed; a credit note reduces a sale, which the CSV shows by a minus sign.
/// </summary>
public sealed record RegisterRow(DateOnly Date, string Number, string Kind, string Direction, string OrderNumber, string CounterpartyName, string? CounterpartyGstin,
    string? PlaceOfSupplyCode, decimal Taxable, decimal Shipping, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

/// <summary>Sums over the whole period (not just the page). Credit notes count as minus.</summary>
public sealed record RegisterTotals(int Documents, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

/// <summary>Sales are the documents the reader issued (tax invoices, credit notes as minus), Purchases the ones they received (the platform fee invoices of a seller).</summary>
public sealed record RegisterPage(DateOnly From, DateOnly To, IReadOnlyList<RegisterRow> Rows, int TotalRows, RegisterTotals Sales, RegisterTotals Purchases);

/// <summary>
/// The list of tax documents for the accountant (Tally, Zoho Books, a GST return): who issued what, to whom, for how much, with the GST split.
/// Only documents that carry figures are in it, that is tax invoices, credit notes and platform fee tax invoices issued after the register was added.
/// Dates are the dates in India.
/// </summary>
public interface IInvoiceRegister
{
    /// <summary>The register of one seller: their tax invoices and credit notes (sales) and the platform fee invoices they received (purchases).</summary>
    Task<RegisterPage> ForSellerAsync(long sellerBusinessId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The register of the platform: the platform fee invoices it issued, to every seller or to one.</summary>
    Task<RegisterPage> ForPlatformAsync(long? sellerBusinessId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The same rows without paging, for a file. Returns at most <paramref name="limit"/> rows; <see cref="RegisterPage.TotalRows"/> says how many there are.</summary>
    Task<RegisterPage> AllForSellerAsync(long sellerBusinessId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default);

    Task<RegisterPage> AllForPlatformAsync(long? sellerBusinessId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default);
}
