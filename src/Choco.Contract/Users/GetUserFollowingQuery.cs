using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Lists the users a user follows, most recently followed first.</summary>
/// <param name="UserId">The following user's non-empty GUID.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "userId": "00000000-0000-0000-0000-000000000001", "cursor": null, "limit": 20 }</example>
public sealed record GetUserFollowingQuery(Guid UserId, string? Cursor, int Limit)
    : IQuery<PageResponse<UserSearchItemResponse>>;
