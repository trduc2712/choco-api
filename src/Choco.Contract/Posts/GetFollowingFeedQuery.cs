using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>Lists posts from the accounts the caller follows, newest first.</summary>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "cursor": null, "limit": 20 }</example>
public sealed record GetFollowingFeedQuery(string? Cursor, int Limit) : IQuery<PageResponse<PostResponse>>;
