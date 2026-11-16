using TextileConnect.Domain.Catalogue;

namespace TextileConnect.Application.Catalogue;

/// <summary>One size/colour with its stock. Id is set for an existing variant; leave it out to add one.</summary>
public sealed record SellerVariantInput(long? Id, string? Sku, string? Size, string? Colour, int StockQty, bool IsActive = true);

/// <summary>One price tier: from MinQty pieces the unit price is PricePerUnit.</summary>
public sealed record SellerTierInput(int MinQty, decimal PricePerUnit);

/// <summary>Create/edit request. Publish=false saves a draft (only Title is required); Publish=true applies every publishing rule.</summary>
public sealed record SellerProductRequest(string? Title, long? CategoryId, string? Description, string? Material, string? FabricSpec, string? Season,
    string? HsnCode, decimal WeightPerUnitGrams, int DispatchDays, string? ReturnPolicy, decimal GstRate, int Moq, ProductUnit Unit,
    IReadOnlyList<SellerVariantInput>? Variants, IReadOnlyList<SellerTierInput>? Tiers, bool Publish);

public sealed record SellerVariantResponse(long Id, string Sku, string? Size, string? Colour, int StockQty, bool IsActive);

public sealed record SellerTierResponse(int MinQty, int? MaxQty, decimal PricePerUnit);

/// <summary>Status is Draft, Active, Inactive or PendingReview. IsActive is the on/off toggle state.</summary>
public sealed record SellerProductSummary(long Id, string Title, string Slug, string Status, bool IsActive, int Moq, decimal? PriceFrom, decimal? PriceTo,
    int TotalStock, bool LowStock, int VariantCount, string? ImageUrl, DateTime UpdatedAt);

public sealed record SellerProductDetail(long Id, string Title, string Slug, string Status, bool IsActive, long CategoryId, string? Description, string? Material,
    string? FabricSpec, string? Season, string? HsnCode, decimal WeightPerUnitGrams, int DispatchDays, string? ReturnPolicy, decimal GstRate, int Moq, ProductUnit Unit,
    IReadOnlyList<SellerVariantResponse> Variants, IReadOnlyList<SellerTierResponse> Tiers, bool AdminDeactivated, DateTime UpdatedAt,
    IReadOnlyList<SellerMediaResponse> Media);

/// <summary>A photo of the product for the edit screen. The first one (IsMain) is the main image.</summary>
public sealed record SellerMediaResponse(long Id, string Url, string MediaType, int SortOrder, bool IsMain);
