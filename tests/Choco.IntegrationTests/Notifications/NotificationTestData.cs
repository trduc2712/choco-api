using System.Net.Http.Headers;

using Choco.Domain.Notifications;
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

    public static async Task<Notification> AddNotificationAsync(
        ChocoApiFactory factory, User recipient, User actor, DateTimeOffset createdAt)
    {
        Notification notification = Notification.Create(recipient.Id, actor.Id, NotificationType.Followed, createdAt);
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
