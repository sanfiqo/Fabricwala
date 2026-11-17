using TextileConnect.Domain.Payments;

namespace TextileConnect.Application.Payments;

public interface IFinanceService
{
    Task<PayoutAccount> AddPayoutAccountAsync(long businessId, string linkedAccountId, CancellationToken cancellationToken = default);
    Task<Invoice> CreateInvoiceAsync(long sellerOrderId, string type, CancellationToken cancellationToken = default);
}
