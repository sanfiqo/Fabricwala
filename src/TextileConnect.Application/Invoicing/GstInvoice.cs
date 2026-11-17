namespace TextileConnect.Application.Invoicing;

/// <summary>One party of an invoice. StateCode is the two digit GST code (08 for Rajasthan).</summary>
public sealed record GstParty(string Name, string? Gstin, string Address, string StateName, string StateCode);

public sealed record GstItemInput(string Title, string? Size, string? Colour, string? HsnCode, int Quantity, decimal UnitPrice, decimal GstRate, decimal GstAmount);

public sealed record GstInvoiceInput(string InvoiceNumber, DateTime IssuedAtUtc, GstParty Seller, GstParty Buyer, string DeliveryAddress, string PlaceOfSupplyName,
    string PlaceOfSupplyCode, string OrderNumber, IReadOnlyList<GstItemInput> Items, decimal Shipping, decimal Total, string? Note = null, string? Title = null);

public sealed record GstLine(int Number, string Description, string? HsnCode, int Quantity, decimal UnitPrice, decimal Taxable, decimal GstRate, decimal Tax);

/// <summary>Everything a printed tax invoice shows. Made by <see cref="GstInvoiceBuilder"/>; the PDF only draws it.</summary>
public sealed record GstInvoice(string InvoiceNumber, DateOnly Date, GstParty Seller, GstParty Buyer, string DeliveryAddress, string PlaceOfSupplyName,
    string PlaceOfSupplyCode, string OrderNumber, bool InterState, IReadOnlyList<GstLine> Lines, decimal Taxable, decimal Shipping, decimal Cgst, decimal Sgst,
    decimal Igst, decimal RoundOff, decimal Total, string AmountInWords, string? Note = null, string? Title = null)
{
    public decimal Tax => Cgst + Sgst + Igst;
}

public static class GstInvoiceBuilder
{
    /// <summary>
    /// Goods taxed at the rate and amount frozen on each order line at checkout (nothing is recalculated here), split into CGST and SGST when the
    /// state of the seller is the place of supply and IGST when it is another state. Delivery is shown without GST because the checkout charges none on it
    /// (an open question for the owner's accountant, see docs/INVOICING.md). RoundOff is the difference to the amount the retailer was charged; it is 0 when the order is consistent.
    /// </summary>
    public static GstInvoice Build(GstInvoiceInput input)
    {
        var lines = input.Items.Select((x, i) => new GstLine(i + 1, Describe(x), string.IsNullOrWhiteSpace(x.HsnCode) ? null : x.HsnCode, x.Quantity, x.UnitPrice,
            Money(x.Quantity * x.UnitPrice), x.GstRate, x.GstAmount)).ToList();
        var taxable = lines.Sum(x => x.Taxable);
        var tax = lines.Sum(x => x.Tax);
        var interState = input.Seller.StateCode != input.PlaceOfSupplyCode;
        var half = interState ? 0m : Money(tax / 2m);
        var computed = taxable + input.Shipping + tax;
        return new GstInvoice(input.InvoiceNumber, FinancialYear.IndiaDate(input.IssuedAtUtc), input.Seller, input.Buyer, input.DeliveryAddress, input.PlaceOfSupplyName,
            input.PlaceOfSupplyCode, input.OrderNumber, interState, lines, taxable, input.Shipping, half, interState ? 0m : tax - half, interState ? tax : 0m,
            input.Total - computed, input.Total, IndianCurrencyWords.Of(input.Total), input.Note, input.Title);
    }

    private static string Describe(GstItemInput item)
    {
        var detail = string.Join(" / ", new[] { item.Size, item.Colour }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return detail.Length == 0 ? item.Title : $"{item.Title} ({detail})";
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public static class InvoiceNumbers
{
    /// <summary>A GST invoice number has at most 16 characters made of letters, digits, / and -. This one is seller id, financial year and a running number: 12/2627/00001.</summary>
    public static string Format(long sellerBusinessId, string financialYear, int number) => Format(sellerBusinessId.ToString(System.Globalization.CultureInfo.InvariantCulture), financialYear, number);

    public static string Format(string series, string financialYear, int number)
    {
        var text = $"{series}/{financialYear}/{number:D5}";
        if (text.Length > 16) throw new InvalidOperationException($"Invoice number {text} is longer than the 16 characters the GST rules allow.");
        return text;
    }
}

public static class CreditNoteSplit
{
    /// <summary>
    /// A refund is an amount the retailer gets back, GST included. The credit note takes the same share of GST out of it as the invoice carried:
    /// tax = refund x (tax of the invoice / total of the invoice), the rest is the taxable value.
    /// </summary>
    public static (decimal Taxable, decimal Tax) Split(decimal refund, decimal invoiceTax, decimal invoiceTotal)
    {
        if (invoiceTotal <= 0m) return (refund, 0m);
        var tax = decimal.Round(refund * invoiceTax / invoiceTotal, 2, MidpointRounding.AwayFromZero);
        return (refund - tax, tax);
    }
}
