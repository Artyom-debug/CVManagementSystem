namespace Application.Common.Models;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasNextPage);
