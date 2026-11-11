using Choco.Domain.Notifications;

namespace Choco.Domain.UnitTests.Notifications;

public sealed class NotificationTests
{
    private static readonly Guid RecipientId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = new("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReadAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterReadAt = new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ReturnsUnreadNotification()
    {
        Notification notification = Notification.Create(RecipientId, ActorId, NotificationType.Followed, CreatedAt);

        notification.Id.Should().NotBeEmpty();
        notification.RecipientId.Should().Be(RecipientId);
        notification.ActorId.Should().Be(ActorId);
        notification.Type.Should().Be(NotificationType.Followed);
        notification.CreatedAt.Should().Be(CreatedAt);
        notification.ReadAt.Should().BeNull();
        notification.PostId.Should().BeNull();
        notification.CommentId.Should().BeNull();
    }

    [Fact]
    public void Create_WithCommentId_SetsCommentId()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000003");
        Guid commentId = new("00000000-0000-0000-0000-000000000004");

        Notification notification = Notification.Create(
            RecipientId, ActorId, NotificationType.PostCommented, CreatedAt, postId, commentId);

        notification.PostId.Should().Be(postId);
        notification.CommentId.Should().Be(commentId);
    }

    [Fact]
    public void Create_WithPostId_SetsPostId()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000003");

        Notification notification = Notification.Create(
            RecipientId, ActorId, NotificationType.PostLiked, CreatedAt, postId);

        notification.Type.Should().Be(NotificationType.PostLiked);
        notification.PostId.Should().Be(postId);
    }

    [Fact]
    public void MarkRead_WhenUnread_SetsReadAt()
    {
        Notification notification = Notification.Create(RecipientId, ActorId, NotificationType.Followed, CreatedAt);

        notification.MarkRead(ReadAt);

        notification.ReadAt.Should().Be(ReadAt);
    }

    [Fact]
    public void MarkRead_WhenAlreadyRead_KeepsFirstReadAt()
    {
        Notification notification = Notification.Create(RecipientId, ActorId, NotificationType.Followed, CreatedAt);
        notification.MarkRead(ReadAt);

        notification.MarkRead(LaterReadAt);

        notification.ReadAt.Should().Be(ReadAt);
    }
}
