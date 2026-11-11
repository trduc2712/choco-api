using System.Net.Http.Headers;

using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Notifications;

internal static class NotificationTestData
{
    public const string CallerIdentityId = "identity-id";

    public static async Task<User> AddUserAsync(ChocoApiFactory factory, string identityId, string username)
    {
        User user = User.Create(identityId, username, null, null);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    public static async Task<Post> AddPostAsync(ChocoApiFactory factory, User author, string content = "content")
    {
        Post post = Post.Create(content, author.Id);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(post);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    public static async Task<Comment> AddCommentAsync(
        ChocoApiFactory factory, Post post, User author, string content = "comment content")
    {
        Comment comment = Comment.Create(post.Id, author.Id, content);
        comment.ClearDomainEvents();
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(comment);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return comment;
    }

    public static async Task DeletePostAsync(ChocoApiFactory factory, Post post)
    {
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static async Task DeleteCommentAsync(ChocoApiFactory factory, Comment comment)
    {
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Comment>().Single(candidate => candidate.Id == comment.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static async Task<Notification> AddNotificationAsync(
        ChocoApiFactory factory,
        User recipient,
        User actor,
        DateTimeOffset createdAt,
        NotificationType type = NotificationType.Followed,
        Post? post = null,
        Comment? comment = null)
    {
        Notification notification = Notification.Create(
            recipient.Id, actor.Id, type, createdAt, post?.Id, comment?.Id);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(notification);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return notification;
    }

    public static Task<List<Notification>> GetNotificationsAsync(ChocoApiFactory factory, Guid recipientId)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<Notification>().AsNoTracking()
            .Where(notification => notification.RecipientId == recipientId)
            .OrderBy(notification => notification.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    public static async Task DeleteUserAsync(ChocoApiFactory factory, User user)
    {
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static HttpRequestMessage CreateRequest(
        HttpMethod method, string uri, string subject = CallerIdentityId)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: subject));

        return request;
    }
}
