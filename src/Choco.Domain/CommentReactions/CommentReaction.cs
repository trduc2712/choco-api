using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.CommentReactions;

public sealed class CommentReaction : AggregateRoot
{
    public const int TypeMaxLength = 16;

    public Guid CommentId { get; private set; }
    public Guid UserId { get; private set; }
    public CommentReactionType Type { get; private set; }
    public DateTimeOffset ReactedAt { get; private set; }

    private CommentReaction() { }

    private CommentReaction(Guid id, Guid commentId, Guid userId, CommentReactionType type, DateTimeOffset reactedAt)
        : base(id)
    {
        CommentId = commentId;
        UserId = userId;
        Type = type;
        ReactedAt = reactedAt;
    }

    public static CommentReaction Create(Guid commentId, Guid userId, CommentReactionType type, DateTimeOffset now)
    {
        if (commentId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(commentId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        CommentReaction reaction = new(Guid.NewGuid(), commentId, userId, type, now);
        reaction.RaiseLikedIfLike();

        return reaction;
    }

    public void ChangeTo(CommentReactionType type, DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (Type == type)
        {
            return;
        }

        Type = type;
        ReactedAt = now;
        RaiseLikedIfLike();
    }

    private void RaiseLikedIfLike()
    {
        if (Type == CommentReactionType.Like)
        {
            RaiseDomainEvent(new CommentLiked(CommentId, UserId, ReactedAt));
        }
    }
}
