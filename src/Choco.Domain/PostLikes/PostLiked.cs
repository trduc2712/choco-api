using Choco.Domain.Primitives.Events;

namespace Choco.Domain.PostLikes;

public sealed record PostLiked(Guid PostId, Guid LikerId, DateTimeOffset LikedAt) : IDomainEvent;
