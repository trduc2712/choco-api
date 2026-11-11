using Choco.Application.Notifications;
using Choco.Contract.Notifications;
using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Notifications;

public sealed class NotificationPagesTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReadAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadPosition_WithoutCursor_ReturnsNull()
    {
        (DateTimeOffset CreatedAt, Guid NotificationId)? position = NotificationPages.ReadPosition(null);

        position.Should().BeNull();
    }

    [Fact]
    public void ReadPosition_WithValidCursor_ReturnsItsPosition()
    {
        Guid notificationId = new("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f");
        string cursor = new NotificationCursor(CreatedAt, notificationId).Encode();

        (DateTimeOffset CreatedAt, Guid NotificationId)? position = NotificationPages.ReadPosition(cursor);

        position.Should().Be((CreatedAt, notificationId));
    }

    [Fact]
    public void ToPage_WithoutEntries_ReturnsEmptyPageWithoutNextCursor()
    {
        var page = NotificationPages.ToPage([], 2);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ToPage_WithAtMostLimitEntries_ReturnsAllWithoutNextCursor(int count)
    {
        IReadOnlyList<NotificationEntry> entries = Entries(count);

        var page = NotificationPages.ToPage(entries, 2);

        page.Items.Select(item => item.Id).Should().Equal(entries.Select(entry => entry.Notification.Id));
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitEntries_ReturnsLimitEntriesAndCursorOfTheLastReturned()
    {
        IReadOnlyList<NotificationEntry> entries = Entries(3);

        var page = NotificationPages.ToPage(entries, 2);

        page.Items.Select(item => item.Id).Should().Equal(entries[0].Notification.Id, entries[1].Notification.Id);
        NotificationCursor.TryDecode(page.NextCursor, out NotificationCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new NotificationCursor(entries[1].Notification.CreatedAt, entries[1].Notification.Id));
    }

    [Fact]
    public void ToResponse_MapsTheNotificationAndItsActor()
    {
        User actor = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        Notification notification = Notification.Create(
            new Guid("00000000-0000-0000-0000-000000000001"), actor.Id, NotificationType.Followed, CreatedAt);

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor));

        response.Should().Be(new NotificationResponse(
            notification.Id,
            "Followed",
            new NotificationActorResponse(actor.Id, "username", "Display Name", "https://example.com/avatar.png"),
            null,
            null,
            null,
            null,
            CreatedAt,
            false));
    }

    [Fact]
    public void ToResponse_WithPost_MapsThePostIdAndExcerpt()
    {
        User actor = User.Create("identity-id", "username", "Display Name", null);
        Post post = Post.Create("post content", new Guid("00000000-0000-0000-0000-000000000004"));
        Notification notification = Notification.Create(
            new Guid("00000000-0000-0000-0000-000000000001"), actor.Id, NotificationType.PostLiked, CreatedAt, post.Id);

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor, post));

        response.Type.Should().Be("PostLiked");
        response.PostId.Should().Be(post.Id);
        response.PostExcerpt.Should().Be("post content");
        response.CommentId.Should().BeNull();
        response.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public void ToResponse_WithPostAndComment_MapsBothIdsAndExcerpts()
    {
        User actor = User.Create("identity-id", "username", "Display Name", null);
        Post post = Post.Create("post content", new Guid("00000000-0000-0000-0000-000000000004"));
        Comment comment = Comment.Create(post.Id, actor.Id, "comment content");
        Notification notification = Notification.Create(
            new Guid("00000000-0000-0000-0000-000000000001"),
            actor.Id,
            NotificationType.PostCommented,
            CreatedAt,
            post.Id,
            comment.Id);

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor, post, comment));

        response.PostId.Should().Be(post.Id);
        response.PostExcerpt.Should().Be("post content");
        response.CommentId.Should().Be(comment.Id);
        response.CommentExcerpt.Should().Be("comment content");
    }

    [Fact]
    public void ToResponse_WithDeletedPostAndComment_KeepsTheIdsAndLeavesTheExcerptsEmpty()
    {
        Guid postId = new("00000000-0000-0000-0000-000000000003");
        Guid commentId = new("00000000-0000-0000-0000-000000000005");
        User actor = User.Create("identity-id", "username", "Display Name", null);
        Notification notification = Notification.Create(
            new Guid("00000000-0000-0000-0000-000000000001"),
            actor.Id,
            NotificationType.PostCommented,
            CreatedAt,
            postId,
            commentId);

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor));

        response.PostId.Should().Be(postId);
        response.PostExcerpt.Should().BeNull();
        response.CommentId.Should().Be(commentId);
        response.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public void ToResponse_WithReadNotification_MarksItRead()
    {
        User actor = User.Create("identity-id", "username", "Display Name", null);
        Notification notification = Notification.Create(
            new Guid("00000000-0000-0000-0000-000000000001"), actor.Id, NotificationType.Followed, CreatedAt);
        notification.MarkRead(ReadAt);

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor));

        response.IsRead.Should().BeTrue();
    }

    private static List<NotificationEntry> Entries(int count)
    {
        return Enumerable.Range(1, count)
            .Select(number => new NotificationEntry(
                Notification.Create(
                    new Guid("00000000-0000-0000-0000-000000000001"),
                    new Guid(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
                    NotificationType.Followed,
                    CreatedAt.AddHours(-number)),
                User.Create($"identity-id-{number}", $"username-{number}", "Display Name", null)))
            .ToList();
    }
}
