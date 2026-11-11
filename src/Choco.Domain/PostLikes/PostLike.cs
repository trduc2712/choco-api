using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.PostLikes;

public sealed class PostLike : AggregateRoot
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset LikedAt { get; private set; }

    private PostLike() { }

    private PostLike(Guid id, Guid postId, Guid userId, DateTimeOffset likedAt)
        : base(id)
    {
        PostId = postId;
        UserId = userId;
        LikedAt = likedAt;
    }

    public static PostLike Create(Guid postId, Guid userId, DateTimeOffset now)
    {
        PostLike like = new(Guid.NewGuid(), postId, userId, now);
        like.RaiseDomainEvent(new PostLiked(postId, userId, now));

        return like;
    }
}
