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
