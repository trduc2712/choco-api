using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;

namespace Choco.Domain.UserFollows;

public interface IUserFollowRepository : IRepository<UserFollow>
{
    Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken);
    Task RemoveIfPresentAsync(Guid followerId, Guid followedUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListFollowersForReadAsync(Guid userId, Guid? after, int count, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListFollowingForReadAsync(Guid userId, Guid? after, int count, CancellationToken cancellationToken);
}
