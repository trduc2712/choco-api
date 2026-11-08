namespace Choco.Contract.Abstractions;

/// <summary>
/// One page of a list, ordered according to the endpoint.
/// </summary>
/// <param name="Items">The items on this page.</param>
/// <param name="NextCursor">Pass it as cursor to get the next page; null when there are no more items.</param>
/// <example>{ "items": [], "nextCursor": null }</example>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
