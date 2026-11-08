using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Lists the users who follow a user, most recent follower first.</summary>
/// <param name="UserId">The followed user's non-empty GUID.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "userId": "00000000-0000-0000-0000-000000000001", "cursor": null, "limit": 20 }</example>
public sealed record GetUserFollowersQuery(Guid UserId, string? Cursor, int Limit)
    : IQuery<PageResponse<UserSearchItemResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
