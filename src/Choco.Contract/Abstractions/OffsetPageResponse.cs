namespace Choco.Contract.Abstractions;

/// <summary>
/// One numbered page of a list, with the size of the whole list.
/// </summary>
/// <param name="Items">The items on this page. Empty when the page is past the last one.</param>
/// <param name="Page">The page number, starting at 1.</param>
/// <param name="PageSize">The number of items a full page holds.</param>
/// <param name="TotalCount">How many items match the filters across all pages.</param>
/// <example>{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }</example>
public sealed record OffsetPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
