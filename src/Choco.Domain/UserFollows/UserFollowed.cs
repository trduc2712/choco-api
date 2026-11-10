using Choco.Domain.Primitives.Events;

namespace Choco.Domain.UserFollows;

public sealed record UserFollowed(Guid FollowerId, Guid FollowedUserId, DateTimeOffset FollowedAt) : IDomainEvent;
