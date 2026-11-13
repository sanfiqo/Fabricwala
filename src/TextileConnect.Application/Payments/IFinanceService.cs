using TextileConnect.Domain.Payments;

namespace TextileConnect.Application.Payments;

public interface IFinanceService
{
    Task<PayoutAccount> AddPayoutAccountAsync(long businessId, string linkedAccountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Settlement>> ReleaseDueSettlementsAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<Refund> CreateRefundAsync(long paymentId, long sellerOrderId, decimal amount, string reason, CancellationToken cancellationToken = default);
    Task<Invoice> CreateInvoiceAsync(long sellerOrderId, string type, CancellationToken cancellationToken = default);
}
