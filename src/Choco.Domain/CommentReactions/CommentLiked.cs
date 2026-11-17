using Choco.Domain.Primitives.Events;

namespace Choco.Domain.CommentReactions;

public sealed record CommentLiked(Guid CommentId, Guid LikerId, DateTimeOffset LikedAt) : IDomainEvent;
