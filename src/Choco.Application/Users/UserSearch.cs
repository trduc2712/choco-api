using Choco.Contract.Users;
using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserSearch
{
    public const int PageSize = 10;

    public static Task<IReadOnlyList<UserSearchMatch>> ReadAsync(
        IUserRepository users,
        Guid callerId,
        string text,
        (int Rank, string Username, Guid Id)? after,
        int count,
        CancellationToken cancellationToken)
    {
        return text.Length == 0
            ? Task.FromResult<IReadOnlyList<UserSearchMatch>>([])
            : users.SearchForReadAsync(text, callerId, after, count, cancellationToken);
    }

    public static UserSearchItemResponse ToResponse(this UserSearchMatch match, IReadOnlySet<Guid> followedUserIds) =>
        new(match.Username, match.DisplayName, match.AvatarUrl, followedUserIds.Contains(match.Id));
}
