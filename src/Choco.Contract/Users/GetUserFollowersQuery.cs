using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Lists accounts that follow a user.</summary>
/// <param name="UserId">The id of the user whose list is requested.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">Page size from 1 to 50.</param>
/// <example>{"userId":"00000000-0000-0000-0000-000000000001","cursor":null,"limit":20}</example>
public sealed record GetUserFollowersQuery(Guid UserId, string? Cursor, int Limit)
    : IQuery<PageResponse<UserFollowItemResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
