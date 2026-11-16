namespace TextileConnect.Api;

public sealed record CategoryResponse(long Id, long? ParentId, string Name, string Slug, int SortOrder);
