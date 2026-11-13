using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Payments;
using TextileConnect.Domain.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Infrastructure.Payments;

public sealed class EfFinanceService(MarketplaceDbContext db) : IFinanceService
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
    { var invoice = new Invoice { SellerOrderId = sellerOrderId, InvoiceNumber = $"TC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20], InvoiceType = type }; db.Invoices.Add(invoice); await db.SaveChangesAsync(cancellationToken); return invoice; }
}
