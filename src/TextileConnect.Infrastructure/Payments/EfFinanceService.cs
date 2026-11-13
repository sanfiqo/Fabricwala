using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Payments;
using TextileConnect.Application.Files;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

public sealed class EfFinanceService(MarketplaceDbContext db, IFileStorage storage) : IFinanceService
{
    public async Task<PayoutAccount> AddPayoutAccountAsync(long businessId, string linkedAccountId, CancellationToken cancellationToken = default)
    {
        var account = await db.PayoutAccounts.SingleOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);
        if (account is null) { account = new PayoutAccount { BusinessId = businessId, GatewayLinkedAccountId = linkedAccountId }; db.PayoutAccounts.Add(account); }
        else account.GatewayLinkedAccountId = linkedAccountId;
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<IReadOnlyList<Settlement>> ReleaseDueSettlementsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var due = await db.Settlements.Where(x => x.Status == "Pending" && x.ReleaseAfter <= now).ToListAsync(cancellationToken);
        foreach (var settlement in due) { settlement.Status = "Released"; settlement.SettledAt = now; }
        await db.SaveChangesAsync(cancellationToken);
        return due;
    }

    public async Task<Refund> CreateRefundAsync(long paymentId, long sellerOrderId, decimal amount, string reason, CancellationToken cancellationToken = default)
    { var refund = new Refund { PaymentId = paymentId, SellerOrderId = sellerOrderId, Amount = amount, Reason = reason }; db.Refunds.Add(refund); await db.SaveChangesAsync(cancellationToken); return refund; }

    public async Task<Invoice> CreateInvoiceAsync(long sellerOrderId, string type, CancellationToken cancellationToken = default)
    {
        var sellerOrder = await db.SellerOrders.Include(x => x.Order).Include(x => x.Items).SingleAsync(x => x.Id == sellerOrderId, cancellationToken);
        var invoice = new Invoice { SellerOrderId = sellerOrderId, InvoiceNumber = $"TC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20], InvoiceType = type };
        var pdf = InvoicePdf.Create(invoice, sellerOrder);
        await using var content = new MemoryStream(pdf);
        invoice.FileKey = await storage.SavePrivateAsync(content, $"{invoice.InvoiceNumber}.pdf", cancellationToken);
        db.Invoices.Add(invoice); await db.SaveChangesAsync(cancellationToken); return invoice;
    }

    private static class InvoicePdf
    {
        public static byte[] Create(Invoice invoice, Domain.Orders.SellerOrder order)
        {
            var text = $"TextileConnect\\nInvoice {invoice.InvoiceNumber}\\nType: {invoice.InvoiceType}\\nSeller order: {order.Id}\\nAmount: INR {order.SubTotal:0.00}";
            var stream = $"BT /F1 12 Tf 50 760 Td ({text.Replace("\\n", ") Tj 0 -18 Td (").Replace("(", "\\(").Replace(")", "\\)")}) Tj ET";
            var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {stream.Length} >>\\nstream\\n{stream}\\nendstream" };
            using var output = new MemoryStream(); using var writer = new StreamWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
            writer.Write("%PDF-1.4\\n"); var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Length; i++) { writer.Flush(); offsets.Add(output.Position); writer.Write($"{i + 1} 0 obj\\n{objects[i]}\\nendobj\\n"); }
            writer.Flush(); var xref = output.Position; writer.Write($"xref\\n0 {objects.Length + 1}\\n0000000000 65535 f \\n");
            for (var i = 1; i < offsets.Count; i++) writer.Write($"{offsets[i]:D10} 00000 n \\n");
            writer.Write($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\\nstartxref\\n{xref}\\n%%EOF"); writer.Flush(); return output.ToArray();
        }
    }
}
