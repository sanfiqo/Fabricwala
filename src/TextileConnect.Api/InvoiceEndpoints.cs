using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Files;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/invoices/seller-orders/{sellerOrderId:long}/download", async (long sellerOrderId, string? type, ICurrentBusiness current, MarketplaceDbContext db, IFinanceService finance, IFileStorage storage, CancellationToken ct) =>
        {
            var invoiceType = string.IsNullOrWhiteSpace(type) ? "SellerToRetailer" : type;
            if (invoiceType is not ("SellerToRetailer" or "PlatformCommission")) return Results.BadRequest();
            var businessId = current.RequireBusinessId();
            var sellerOrder = await db.SellerOrders.Include(x => x.Order).SingleOrDefaultAsync(x => x.Id == sellerOrderId && (x.ManufacturerBusinessId == businessId || x.Order!.RetailerBusinessId == businessId), ct);
            if (sellerOrder is null || (invoiceType == "PlatformCommission" && sellerOrder.ManufacturerBusinessId != businessId)) return Results.NotFound(); // the commission invoice is between the platform and the seller
            var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.SellerOrderId == sellerOrderId && x.InvoiceType == invoiceType, ct) ?? await finance.CreateInvoiceAsync(sellerOrderId, invoiceType, ct);
            var stream = await storage.OpenPrivateAsync(invoice.FileKey!, ct);
            return Results.File(stream, "application/pdf", $"{invoice.InvoiceNumber.Replace('/', '-')}.pdf");
        }).RequireAuthorization("ApprovedBusiness");

        // The credit note of one refund, for the seller or the retailer of that order. 409 while the refund has not gone through, or when no tax invoice was issued to correct.
        app.MapGet("/api/v1/invoices/refunds/{refundId:long}/credit-note", async (long refundId, ICurrentBusiness current, MarketplaceDbContext db, IFinanceService finance, IFileStorage storage, CancellationToken ct) =>
        {
            var businessId = current.RequireBusinessId();
            var allowed = await (from refund in db.Refunds.AsNoTracking()
                                 join sellerOrder in db.SellerOrders.AsNoTracking() on refund.SellerOrderId equals sellerOrder.Id
                                 join order in db.Orders.AsNoTracking() on sellerOrder.OrderId equals order.Id
                                 where refund.Id == refundId && (sellerOrder.ManufacturerBusinessId == businessId || order.RetailerBusinessId == businessId)
                                 select refund.Id).AnyAsync(ct);
            if (!allowed) return Results.NotFound();
            var note = await finance.CreateCreditNoteAsync(refundId, ct);
            var stream = await storage.OpenPrivateAsync(note.FileKey!, ct);
            return Results.File(stream, "application/pdf", $"{note.InvoiceNumber.Replace('/', '-')}.pdf");
        }).RequireAuthorization("ApprovedBusiness");
    }
}
