using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.UserFollows;

public interface IUserFollowRepository : IRepository<UserFollow>
{
    Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken);
    Task RemoveIfPresentAsync(Guid followerId, Guid followedUserId, CancellationToken cancellationToken);
    Task<UserFollowSummary> GetSummaryForReadAsync(Guid userId, Guid viewerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserFollowEntry>> ListFollowersForReadAsync(
        Guid userId,
        (DateTimeOffset FollowedAt, Guid FollowId)? after,
        int count,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserFollowEntry>> ListFollowingForReadAsync(
        Guid userId,
        (DateTimeOffset FollowedAt, Guid FollowId)? after,
        int count,
        CancellationToken cancellationToken);
}
