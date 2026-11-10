using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;

namespace Choco.Application.Users;

internal static class UserFollowPages
{
    public static (DateTimeOffset FollowedAt, Guid FollowId)? ReadPosition(string? cursor)
    {
        return FollowCursor.TryDecode(cursor, out FollowCursor? position)
            ? (position!.FollowedAt, position.FollowId)
            : null;
    }

    public static Guid[] PageUserIds(IReadOnlyList<UserFollowEntry> entries, int limit)
    {
        return entries.Take(limit).Select(entry => entry.User.Id).ToArray();
    }

    public static PageResponse<UserSearchItemResponse> ToPage(
        IReadOnlyList<UserFollowEntry> entries, int limit, IReadOnlySet<Guid> followedUserIds)
    {
        List<UserFollowEntry> page = entries.Take(limit).ToList();
        string? nextCursor = entries.Count > limit
            ? new FollowCursor(page[^1].FollowedAt, page[^1].FollowId).Encode()
            : null;

        return new PageResponse<UserSearchItemResponse>(
            page.Select(entry => new UserSearchItemResponse(
                entry.User.Id,
                entry.User.Username,
                entry.User.DisplayName,
                entry.User.AvatarUrl,
                followedUserIds.Contains(entry.User.Id))).ToList(),
            nextCursor);
    }
}
