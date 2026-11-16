namespace TextileConnect.Application.Common;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
