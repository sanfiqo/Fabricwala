using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TextileConnect.Application.Invoicing;

namespace TextileConnect.Infrastructure.Payments;

/// <summary>Draws a <see cref="GstInvoice"/> as an A4 PDF. It decides nothing about money: every number comes from the model.</summary>
public static class GstInvoicePdf
{
    static GstInvoicePdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(GstInvoice invoice) => Build(invoice).GeneratePdf();

    /// <summary>The same page as PNG images, for looking at the layout.</summary>
    public static IEnumerable<byte[]> RenderImages(GstInvoice invoice) => Build(invoice).GenerateImages();

    private static string Rs(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("en-IN"));

    private static Document Build(GstInvoice invoice) => Document.Create(document => document.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(28);
        page.DefaultTextStyle(x => x.FontSize(9));

        page.Header().Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(invoice.Title ?? "TAX INVOICE").Bold().FontSize(18);
                row.RelativeItem().AlignRight().Text("Original for recipient").FontSize(8);
            });
            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text(text => { text.Span("Invoice no: ").SemiBold(); text.Span(invoice.InvoiceNumber); });
                row.RelativeItem().Text(text => { text.Span("Date: ").SemiBold(); text.Span(invoice.Date.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)); });
                row.RelativeItem().AlignRight().Text(text => { text.Span("Order: ").SemiBold(); text.Span(invoice.OrderNumber); });
            });
            column.Item().PaddingTop(6).LineHorizontal(1);
        });

        page.Content().PaddingTop(8).Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Party(c, "Sold by", invoice.Seller));
                row.ConstantItem(12);
                row.RelativeItem().Element(c => Party(c, "Billed to", invoice.Buyer));
            });
            column.Item().Text(text =>
            {
                if (invoice.DeliveryAddress.Length > 0) { text.Span("Deliver to: ").SemiBold(); text.Span(invoice.DeliveryAddress + ". "); }
                text.Span("Place of supply: ").SemiBold(); text.Span($"{invoice.PlaceOfSupplyName} ({invoice.PlaceOfSupplyCode}). ");
                text.Span(invoice.InterState ? "Supply between states: IGST applies." : "Supply within the state: CGST and SGST apply.");
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(20); c.RelativeColumn(4); c.ConstantColumn(50); c.ConstantColumn(34); c.ConstantColumn(54); c.ConstantColumn(62);
                    c.ConstantColumn(34); c.ConstantColumn(54); c.ConstantColumn(66);
                });
                table.Header(h =>
                {
                    foreach (var title in new[] { "#", "Description", "HSN/SAC", "Qty", "Rate", "Taxable", "GST %", "GST", "Amount" })
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(title).SemiBold();
                });
                foreach (var line in invoice.Lines)
                {
                    table.Cell().Padding(3).Text(line.Number.ToString());
                    table.Cell().Padding(3).Text(line.Description);
                    table.Cell().Padding(3).Text(line.HsnCode ?? "-");
                    table.Cell().Padding(3).AlignRight().Text(line.Quantity.ToString());
                    table.Cell().Padding(3).AlignRight().Text(Rs(line.UnitPrice));
                    table.Cell().Padding(3).AlignRight().Text(Rs(line.Taxable));
                    table.Cell().Padding(3).AlignRight().Text(line.GstRate.ToString("0.##"));
                    table.Cell().Padding(3).AlignRight().Text(Rs(line.Tax));
                    table.Cell().Padding(3).AlignRight().Text(Rs(line.Taxable + line.Tax));
                }
            });

            column.Item().AlignRight().Width(240).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(90); });
                void Row(string label, decimal value, bool bold = false)
                {
                    var left = table.Cell().Padding(2).Text(label); var right = table.Cell().Padding(2).AlignRight().Text("Rs " + Rs(value));
                    if (bold) { left.Bold(); right.Bold(); }
                }
                Row("Taxable value", invoice.Taxable);
                if (invoice.InterState) Row("IGST", invoice.Igst);
                else { Row("CGST", invoice.Cgst); Row("SGST", invoice.Sgst); }
                Row("Delivery charge", invoice.Shipping);
                if (invoice.RoundOff != 0) Row("Round off", invoice.RoundOff);
                Row("Total", invoice.Total, bold: true);
            });

            column.Item().Text(text => { text.Span("Amount in words: ").SemiBold(); text.Span(invoice.AmountInWords); });
        });

        page.Footer().Column(column =>
        {
            column.Item().LineHorizontal(0.5f);
            column.Item().PaddingTop(3).Text(invoice.Note ?? "This is a computer generated invoice and needs no signature. Sold through the Fabricwala marketplace. The platform fee is charged to the seller and is not part of this invoice.").FontSize(7.5f);
        });
    }));

    private static void Party(IContainer container, string heading, GstParty party) => container.Border(0.5f).Padding(5).Column(column =>
    {
        column.Item().Text(heading).FontSize(8).FontColor(Colors.Grey.Darken1);
        column.Item().Text(party.Name).Bold();
        column.Item().Text(party.Address);
        column.Item().Text($"State: {party.StateName} ({party.StateCode})");
        column.Item().Text(party.Gstin is null ? "GSTIN: not registered" : "GSTIN: " + party.Gstin);
    });

    /// <summary>The platform fee note for a seller. It is a statement, not a tax invoice: no GST is charged on the fee yet (open question in docs/INVOICING.md).</summary>
    public static byte[] RenderFeeStatement(string number, string orderNumber, string sellerName, decimal goods, decimal ratePercent, decimal fee)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Content().Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("PLATFORM FEE STATEMENT").Bold().FontSize(16);
                column.Item().Text("Not a tax invoice.").FontSize(9);
                column.Item().Text($"Statement no: {number}");
                column.Item().Text($"Seller: {sellerName}");
                column.Item().Text($"Order: {orderNumber}");
                column.Item().Text($"Goods value: Rs {Rs(goods)}");
                column.Item().Text($"Platform fee at {ratePercent:0.##}%: Rs {Rs(fee)}");
                column.Item().Text("The fee is taken from the payout of this order.");
            });
        })).GeneratePdf();
    }
}
