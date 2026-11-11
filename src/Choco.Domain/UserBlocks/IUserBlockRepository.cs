using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.UserBlocks;

public interface IUserBlockRepository : IRepository<UserBlock>
{
    Task<bool> AddIfMissingAsync(UserBlock block, CancellationToken cancellationToken);
    Task RemoveIfPresentAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken);
    Task<bool> ExistsForReadAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken);
    Task<bool> ExistsBetweenForReadAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserBlockEntry>> ListBlockedForReadAsync(
        Guid blockerId,
        (DateTimeOffset BlockedAt, Guid BlockId)? after,
        int count,
        CancellationToken cancellationToken);
}
