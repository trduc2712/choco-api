using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UserFollows;

public sealed class UserFollow : AggregateRoot
{
    public Guid FollowerId { get; private set; }
    public Guid FollowedUserId { get; private set; }
    public DateTimeOffset FollowedAt { get; private set; }

    private UserFollow() { }

    private UserFollow(Guid id, Guid followerId, Guid followedUserId, DateTimeOffset followedAt)
        : base(id)
    {
        FollowerId = followerId;
        FollowedUserId = followedUserId;
        FollowedAt = followedAt;
    }

    public static Result<UserFollow> Create(Guid followerId, Guid followedUserId, DateTimeOffset now)
    {
        if (followerId == followedUserId)
        {
            return Result<UserFollow>.Failure(UserFollowErrors.SelfFollow);
        }

        return Result<UserFollow>.Success(new UserFollow(Guid.NewGuid(), followerId, followedUserId, now));
    }
}
