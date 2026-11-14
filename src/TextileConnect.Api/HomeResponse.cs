using TextileConnect.Application.Common;

namespace TextileConnect.Api;

public sealed record HomeResponse(IReadOnlyList<CategoryResponse> Categories, PagedResponse<ProductSummaryResponse> BestSellers);
