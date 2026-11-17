using System.Globalization;
using System.Text;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Common;
using TextileConnect.Application.Invoicing;

namespace TextileConnect.Api;

/// <summary>
/// The register of tax documents for the accountant: <c>GET /api/v1/seller/invoices/register</c> for a manufacturer (their sales and the platform fee invoices they received) and
/// <c>GET /api/v1/admin/invoices/register</c> for the platform (the fee invoices it issued). Query: <c>from</c> and <c>to</c> as yyyy-MM-dd (default: this month so far, India time, at most 366 days),
/// <c>format=csv</c> for a file that opens in Excel, Tally or Zoho (otherwise JSON, paged), and for the admin <c>sellerId</c>. See docs/INVOICING.md.
/// </summary>
public static class InvoiceRegisterEndpoints
{
    private const int MaxCsvRows = 20_000;
    private static readonly TimeSpan India = TimeSpan.FromMinutes(330);

    public static void MapInvoiceRegisterEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seller/invoices/register", async (string? from, string? to, string? format, int? page, int? pageSize, ICurrentBusiness current, IInvoiceRegister register, CancellationToken ct) =>
        {
            if (!TryRange(from, to, out var start, out var end) || !ValidPaging(page, pageSize) || !ValidFormat(format)) return Results.BadRequest();
            var sellerId = current.RequireBusinessId();
            return IsCsv(format)
                ? Csv(await register.AllForSellerAsync(sellerId, start, end, MaxCsvRows + 1, ct), "invoice-register")
                : Results.Ok(await register.ForSellerAsync(sellerId, start, end, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization("ApprovedBusiness").RequireAuthorization(p => p.RequireRole("Manufacturer"));

        app.MapGet("/api/v1/admin/invoices/register", async (string? from, string? to, string? format, long? sellerId, int? page, int? pageSize, IInvoiceRegister register, CancellationToken ct) =>
        {
            if (!TryRange(from, to, out var start, out var end) || !ValidPaging(page, pageSize) || !ValidFormat(format) || sellerId is <= 0) return Results.BadRequest();
            return IsCsv(format)
                ? Csv(await register.AllForPlatformAsync(sellerId, start, end, MaxCsvRows + 1, ct), "platform-fee-register")
                : Results.Ok(await register.ForPlatformAsync(sellerId, start, end, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }

    private static bool ValidPaging(int? page, int? pageSize) => (page ?? 1) is >= 1 and <= 1_000_000 && (pageSize ?? 50) is >= 1 and <= 100;

    private static bool ValidFormat(string? format) => string.IsNullOrEmpty(format) || string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) || IsCsv(format);

    private static bool IsCsv(string? format) => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    private static bool TryRange(string? from, string? to, out DateOnly start, out DateOnly end)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow + India);
        start = default; end = default;
        if (from is null) start = new DateOnly(today.Year, today.Month, 1);
        else if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) return false;
        if (to is null) end = today < start ? start : today;
        else if (!DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) return false;
        return start <= end && end.DayNumber - start.DayNumber <= 365;
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A file with one line per document. A credit note is written with a minus sign so that a column adds up to the net figure. The first bytes tell Excel that it is UTF-8.</summary>
    private static IResult Csv(RegisterPage page, string name)
    {
        if (page.TotalRows > MaxCsvRows) throw new RuleViolationException("register_too_large", $"There are more than {MaxCsvRows} documents in this period. Choose a shorter period.", 400);
        var text = new StringBuilder("Date,Number,Kind,Direction,Order,Party,PartyGstin,PlaceOfSupplyCode,Taxable,Shipping,CGST,SGST,IGST,Total\r\n");
        foreach (var row in page.Rows)
        {
            var sign = row.Kind == "CreditNote" ? -1m : 1m;
            text.Append(string.Join(',', row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), CsvCell.Text(row.Number), row.Kind, row.Direction, CsvCell.Text(row.OrderNumber),
                CsvCell.Text(row.CounterpartyName), CsvCell.Text(row.CounterpartyGstin), CsvCell.Text(row.PlaceOfSupplyCode), Money(sign * row.Taxable), Money(sign * row.Shipping),
                Money(sign * row.Cgst), Money(sign * row.Sgst), Money(sign * row.Igst), Money(sign * row.Total))).Append("\r\n");
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8", $"{name}-{page.From:yyyyMMdd}-{page.To:yyyyMMdd}.csv");
    }
}
