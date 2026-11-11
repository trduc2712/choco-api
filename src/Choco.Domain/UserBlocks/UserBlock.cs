using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UserBlocks;

public sealed class UserBlock : AggregateRoot
{
    public Guid BlockerId { get; private set; }
    public Guid BlockedUserId { get; private set; }
    public DateTimeOffset BlockedAt { get; private set; }

    private UserBlock() { }

    private UserBlock(Guid id, Guid blockerId, Guid blockedUserId, DateTimeOffset blockedAt)
        : base(id)
    {
        BlockerId = blockerId;
        BlockedUserId = blockedUserId;
        BlockedAt = blockedAt;
    }

    public static Result<UserBlock> Create(Guid blockerId, Guid blockedUserId, DateTimeOffset now)
    {
        if (blockerId == blockedUserId)
        {
            return Result<UserBlock>.Failure(UserBlockErrors.SelfBlock);
        }

        return Result<UserBlock>.Success(new UserBlock(Guid.NewGuid(), blockerId, blockedUserId, now));
    }
}
