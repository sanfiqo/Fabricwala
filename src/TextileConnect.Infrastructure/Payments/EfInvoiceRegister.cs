using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Invoicing;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

public sealed class EfInvoiceRegister(MarketplaceDbContext db) : IInvoiceRegister
{
    private static readonly TimeSpan India = TimeSpan.FromMinutes(330);
    private const string TaxInvoice = "SellerToRetailer", FeeInvoice = "PlatformCommission", CreditNote = "CreditNote";

    public Task<RegisterPage> ForSellerAsync(long sellerBusinessId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken cancellationToken = default)
        => PageAsync(Seller(sellerBusinessId), RegisterDirection.Sale, sellerBusinessId, from, to, (page - 1) * pageSize, pageSize, cancellationToken);

    public Task<RegisterPage> ForPlatformAsync(long? sellerBusinessId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken cancellationToken = default)
        => PageAsync(Platform(sellerBusinessId), null, null, from, to, (page - 1) * pageSize, pageSize, cancellationToken);

    public Task<RegisterPage> AllForSellerAsync(long sellerBusinessId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default)
        => PageAsync(Seller(sellerBusinessId), RegisterDirection.Sale, sellerBusinessId, from, to, 0, limit, cancellationToken);

    public Task<RegisterPage> AllForPlatformAsync(long? sellerBusinessId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default)
        => PageAsync(Platform(sellerBusinessId), null, null, from, to, 0, limit, cancellationToken);

    /// <summary>A line before the dates are applied: the stored document with the order it belongs to.</summary>
    private sealed record Line(long Id, string Number, string Type, DateTime IssuedAt, string OrderNumber, long SellerBusinessId, string? CounterpartyName, string? CounterpartyGstin,
        string? PlaceOfSupplyCode, decimal Taxable, decimal Shipping, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

    private IQueryable<Line> Documents() => from invoice in db.Invoices.AsNoTracking()
                                            join sellerOrder in db.SellerOrders.AsNoTracking() on invoice.SellerOrderId equals sellerOrder.Id
                                            join order in db.Orders.AsNoTracking() on sellerOrder.OrderId equals order.Id
                                            where invoice.Total != null
                                            select new Line(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceType, invoice.IssuedAt, order.OrderNumber, sellerOrder.ManufacturerBusinessId, invoice.CounterpartyName,
                                                invoice.CounterpartyGstin, invoice.PlaceOfSupplyCode, invoice.Taxable ?? 0m, invoice.Shipping ?? 0m, invoice.Cgst ?? 0m, invoice.Sgst ?? 0m, invoice.Igst ?? 0m, invoice.Total ?? 0m);

    private IQueryable<Line> Seller(long sellerBusinessId) => Documents().Where(x => x.SellerBusinessId == sellerBusinessId);

    private IQueryable<Line> Platform(long? sellerBusinessId)
        => Documents().Where(x => x.Type == FeeInvoice && (sellerBusinessId == null || x.SellerBusinessId == sellerBusinessId));

    /// <summary>The dates are dates in India: from 00:00 of the first day to 24:00 of the last day, India time.</summary>
    private static (DateTime Start, DateTime End) Range(DateOnly from, DateOnly to)
        => (from.ToDateTime(TimeOnly.MinValue) - India, to.AddDays(1).ToDateTime(TimeOnly.MinValue) - India);

    private static string KindOf(string type) => type switch { CreditNote => "CreditNote", FeeInvoice => "PlatformFeeInvoice", _ => "TaxInvoice" };

    private static RegisterTotals Sum(IEnumerable<Line> lines)
    {
        var count = 0; decimal taxable = 0, cgst = 0, sgst = 0, igst = 0, total = 0;
        foreach (var line in lines)
        {
            var sign = line.Type == CreditNote ? -1m : 1m;
            count++; taxable += sign * line.Taxable; cgst += sign * line.Cgst; sgst += sign * line.Sgst; igst += sign * line.Igst; total += sign * line.Total;
        }
        return new RegisterTotals(count, taxable, cgst, sgst, igst, total);
    }

    private async Task<RegisterPage> PageAsync(IQueryable<Line> source, string? sellerDirection, long? sellerId, DateOnly from, DateOnly to, int skip, int take, CancellationToken cancellationToken)
    {
        var (start, end) = Range(from, to);
        var inRange = source.Where(x => x.IssuedAt >= start && x.IssuedAt < end);
        var total = await inRange.CountAsync(cancellationToken);
        var page = await inRange.OrderBy(x => x.IssuedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        // Totals are over the whole period, so they are worked out from the narrow rows of the period, not from the page.
        var all = await inRange.Select(x => new Line(x.Id, x.Number, x.Type, x.IssuedAt, x.OrderNumber, x.SellerBusinessId, null, null, null, x.Taxable, x.Shipping, x.Cgst, x.Sgst, x.Igst, x.Total)).ToListAsync(cancellationToken);
        // A seller reads fee invoices as purchases; for the platform they are all sales.
        bool IsPurchase(Line line) => sellerDirection is not null && line.Type == FeeInvoice;
        var rows = page.Select(x => new RegisterRow(DateOnly.FromDateTime(x.IssuedAt + India), x.Number, KindOf(x.Type), IsPurchase(x) ? RegisterDirection.Purchase : RegisterDirection.Sale,
            x.OrderNumber, x.CounterpartyName ?? string.Empty, x.CounterpartyGstin, x.PlaceOfSupplyCode, x.Taxable, x.Shipping, x.Cgst, x.Sgst, x.Igst, x.Total)).ToList();
        return new RegisterPage(from, to, rows, total, Sum(all.Where(x => !IsPurchase(x))), Sum(all.Where(IsPurchase)));
    }
}
