using TextileConnect.Domain.Community;
namespace TextileConnect.Application.Community;
public interface ICommunityService
{
    Task<Enquiry> CreateEnquiryAsync(long businessId, long? productId, string description, CancellationToken ct = default);
    Task<Review> AddReviewAsync(long businessId, long productId, int rating, string? comment, CancellationToken ct = default);
    Task<bool> ToggleFavouriteAsync(long businessId, long productId, CancellationToken ct = default);
    Task<GroupBuy> CreateGroupBuyAsync(long businessId, long productId, int targetQuantity, DateTime closesAtUtc, CancellationToken ct = default);
}
