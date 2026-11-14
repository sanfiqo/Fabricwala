namespace TextileConnect.Api;

public sealed record BusinessReviewResponse(BusinessProfileResponse Business, GstCheckResponse GstCheck,
    IReadOnlyList<BusinessDocumentResponse> Documents);
