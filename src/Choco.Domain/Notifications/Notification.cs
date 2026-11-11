using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Notifications;

public sealed class Notification : AggregateRoot
{
    public const int TypeMaxLength = 32;

    public Guid RecipientId { get; private set; }
    public Guid ActorId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? PostId { get; private set; }
    public Guid? CommentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    private Notification(
        Guid id,
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        DateTimeOffset createdAt,
        Guid? postId,
        Guid? commentId)
        : base(id)
    {
        RecipientId = recipientId;
        ActorId = actorId;
        Type = type;
        CreatedAt = createdAt;
        PostId = postId;
        CommentId = commentId;
    }

    public static Notification Create(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        DateTimeOffset now,
        Guid? postId = null,
        Guid? commentId = null)
    {
        return new Notification(Guid.NewGuid(), recipientId, actorId, type, now, postId, commentId);
    }

    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }
}
