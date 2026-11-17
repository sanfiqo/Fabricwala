namespace TextileConnect.Api;

/// <summary>A filter value that is a name (material, season, size, colour) with the number of products it would give.</summary>
public sealed record FacetValue(string Value, int Count);

/// <summary>A filter value that has an id (category, seller city).</summary>
public sealed record FacetId(long Id, string Name, int Count);

public sealed record FacetState(long Id, string Name, string Code, int Count);

public sealed record FacetPriceRange(decimal? Min, decimal? Max);

/// <summary>
/// What the filter sheet needs: the number of matching products and, for each filter, the values with counts.
/// Each list is counted with every other filter applied but not its own, so choosing "Cotton" still shows how many "Wool" products the other filters leave.
/// </summary>
public sealed record ProductFiltersResponse(int Total, IReadOnlyList<FacetId> Categories, IReadOnlyList<FacetValue> Materials, IReadOnlyList<FacetValue> Seasons,
    IReadOnlyList<FacetValue> Sizes, IReadOnlyList<FacetValue> Colours, IReadOnlyList<FacetState> States, IReadOnlyList<FacetId> Cities, FacetPriceRange Price);
