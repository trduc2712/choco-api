using Choco.Domain.UserFollows;

namespace Choco.Application.Users;

internal static class FollowStatus
{
    public static async Task<IReadOnlySet<Guid>> ReadFollowedAsync(
        IUserFollowRepository followRepository,
        Guid viewerId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        return userIds.Count == 0
            ? new HashSet<Guid>()
            : await followRepository.GetFollowedUserIdsForReadAsync(viewerId, userIds, cancellationToken);
    }
}
