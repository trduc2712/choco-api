using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.UserFollows;

public interface IUserFollowRepository : IRepository<UserFollow>
{
    Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken);
    Task RemoveIfPresentAsync(Guid followerId, Guid followedUserId, CancellationToken cancellationToken);
}
