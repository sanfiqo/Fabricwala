using TextileConnect.Domain.Payments;

namespace TextileConnect.Application.Payments;

public interface IFinanceService
{
    Task<PayoutAccount> AddPayoutAccountAsync(long businessId, string linkedAccountId, CancellationToken cancellationToken = default);
    Task<Invoice> CreateInvoiceAsync(long sellerOrderId, string type, CancellationToken cancellationToken = default);

    /// <summary>The credit note for a processed refund, against the tax invoice that was issued for the seller order. One per refund.</summary>
    Task<Invoice> CreateCreditNoteAsync(long refundId, CancellationToken cancellationToken = default);
}
