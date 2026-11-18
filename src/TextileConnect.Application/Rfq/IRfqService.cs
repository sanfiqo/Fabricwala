using TextileConnect.Application.Common;

namespace TextileConnect.Application.Rfq;

/// <param name="Unit">Piece, Set, Metre or Kg; Piece when empty.</param>
/// <param name="ExpiresInDays">1 to 30; 14 when empty.</param>
public sealed record RfqInput(string Title, string? Description, long? CategoryId, int Quantity, string? Unit, decimal? TargetPricePerUnit, string DeliveryPincode, DateTime? NeededBy, int? ExpiresInDays);

public sealed record QuotationInput(decimal PricePerUnit, int Quantity, int LeadTimeDays, string? Note);

/// <summary>One line of a list. Status is the status as it is now: an open request whose time has passed says Expired.</summary>
public sealed record RfqSummary(long Id, string Title, int Quantity, string Unit, string Status, DateTime ExpiresAt, int QuotationCount, string DeliveryCity, string DeliveryState,
    DateTime CreatedAt, long? CategoryId, string? CategoryName, decimal? TargetPricePerUnit, bool? IHaveQuoted);

/// <summary>Who is on the other side, shown only once the request is awarded and only to the two parties.</summary>
public sealed record RfqContact(string Name, string Phone);

/// <summary>
/// A quotation as the reader may see it. The retailer sees every quotation with the name of the manufacturer; a manufacturer sees only their own.
/// Contact is filled for the accepted quotation only.
/// </summary>
public sealed record QuotationView(long Id, long SellerBusinessId, string SellerName, decimal PricePerUnit, int Quantity, decimal Total, int LeadTimeDays, string? Note, string Status,
    DateTime CreatedAt, bool IsMine, RfqContact? Contact);

public sealed record RfqDetail(RfqSummary Summary, string? Description, DateTime? NeededBy, IReadOnlyList<QuotationView> Quotations, RfqContact? BuyerContact);

/// <summary>A line of "my quotations" for a manufacturer: the request and the quotation they sent.</summary>
public sealed record MyQuotationRow(RfqSummary Rfq, QuotationView Quotation);

public sealed record RfqAdminRow(long Id, string Title, string RetailerName, int Quantity, string Status, DateTime ExpiresAt, int QuotationCount, DateTime CreatedAt);

/// <summary>
/// Reverse marketplace: a retailer asks, manufacturers quote, the retailer accepts one. Every method that takes a business id works only on what that business owns or may see;
/// a request of somebody else answers "not found". Rules that fail throw <see cref="RuleViolationException"/> with a code the client can show.
/// </summary>
public interface IRfqService
{
    // retailer
    Task<RfqDetail> CreateAsync(long retailerBusinessId, RfqInput input, CancellationToken cancellationToken = default);
    Task<PagedResponse<RfqSummary>> ListMineAsync(long retailerBusinessId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<RfqDetail?> GetMineAsync(long retailerBusinessId, long rfqId, CancellationToken cancellationToken = default);
    Task<RfqDetail?> CancelAsync(long retailerBusinessId, long rfqId, CancellationToken cancellationToken = default);
    Task<RfqDetail?> AcceptAsync(long retailerBusinessId, long rfqId, long quotationId, CancellationToken cancellationToken = default);

    // manufacturer
    Task<PagedResponse<RfqSummary>> FeedAsync(long sellerBusinessId, long? categoryId, string? q, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<RfqDetail?> GetForSellerAsync(long sellerBusinessId, long rfqId, CancellationToken cancellationToken = default);
    Task<RfqDetail?> QuoteAsync(long sellerBusinessId, long rfqId, QuotationInput input, CancellationToken cancellationToken = default);
    Task<RfqDetail?> WithdrawAsync(long sellerBusinessId, long rfqId, CancellationToken cancellationToken = default);
    Task<PagedResponse<MyQuotationRow>> MyQuotationsAsync(long sellerBusinessId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    // admin
    Task<PagedResponse<RfqAdminRow>> ListAllAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(long rfqId, string? reason, CancellationToken cancellationToken = default);
}
