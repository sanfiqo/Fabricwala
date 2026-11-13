using TextileConnect.Application.Businesses; using TextileConnect.Application.Community;
namespace TextileConnect.Api;
public static class CommunityEndpoints
{
    public static void MapCommunityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization("ApprovedBusiness");
        group.MapPost("/enquiries", async (EnquiryRequest request, ICurrentBusiness current, ICommunityService service, CancellationToken ct) => Results.Ok(await service.CreateEnquiryAsync(current.RequireBusinessId(), request.ProductId, request.Description, ct)));
        group.MapPost("/reviews", async (ReviewRequest request, ICurrentBusiness current, ICommunityService service, CancellationToken ct) => Results.Ok(await service.AddReviewAsync(current.RequireBusinessId(), request.ProductId, request.Rating, request.Comment, ct)));
        group.MapPost("/favourites/{productId:long}", async (long productId, ICurrentBusiness current, ICommunityService service, CancellationToken ct) => Results.Ok(new { favourite = await service.ToggleFavouriteAsync(current.RequireBusinessId(), productId, ct) }));
        group.MapPost("/group-buys", async (GroupBuyRequest request, ICurrentBusiness current, ICommunityService service, CancellationToken ct) => Results.Ok(await service.CreateGroupBuyAsync(current.RequireBusinessId(), request.ProductId, request.TargetQuantity, request.ClosesAtUtc, ct)));
    }
    public sealed record EnquiryRequest(long? ProductId, string Description); public sealed record ReviewRequest(long ProductId, int Rating, string? Comment); public sealed record GroupBuyRequest(long ProductId, int TargetQuantity, DateTime ClosesAtUtc);
}
