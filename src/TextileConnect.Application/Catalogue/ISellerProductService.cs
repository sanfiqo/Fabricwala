using TextileConnect.Application.Common;

namespace TextileConnect.Application.Catalogue;

/// <summary>A seller's own products. Every call is scoped to the seller's business id.</summary>
public interface ISellerProductService
{
    /// <param name="filter">all, low_stock, draft, active or inactive.</param>
    Task<PagedResponse<SellerProductSummary>> ListAsync(long sellerId, string? filter, string? query, int lowStockThreshold, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<SellerProductDetail?> GetAsync(long sellerId, long productId, CancellationToken cancellationToken = default);

    /// <summary>Throws FluentValidation.ValidationException with field names when the request breaks a rule.</summary>
    Task<SellerProductDetail> CreateAsync(long sellerId, SellerProductRequest request, CancellationToken cancellationToken = default);

    Task<SellerProductDetail?> UpdateAsync(long sellerId, long productId, SellerProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>The on/off toggle. Turning on a draft or inactive product publishes it (all rules apply).</summary>
    Task<SellerProductDetail?> SetActiveAsync(long sellerId, long productId, bool active, CancellationToken cancellationToken = default);
}
