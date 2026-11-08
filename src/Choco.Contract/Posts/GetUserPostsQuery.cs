using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>Lists a user's posts, newest first.</summary>
/// <param name="UserId">The user's non-empty GUID.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "userId": "00000000-0000-0000-0000-000000000001", "cursor": null, "limit": 20 }</example>
public sealed record GetUserPostsQuery(Guid UserId, string? Cursor, int Limit) : IQuery<PageResponse<PostResponse>>;
