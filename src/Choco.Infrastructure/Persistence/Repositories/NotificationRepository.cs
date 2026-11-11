using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public Task<Notification?> GetByIdForRecipientAsync(
        Guid id, Guid recipientId, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(
            notification => notification.Id == id && notification.RecipientId == recipientId, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationEntry>> ListForReadAsync(
        Guid recipientId,
        (DateTimeOffset CreatedAt, Guid NotificationId)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(recipientId);

        IQueryable<Notification> notifications = DbSet.AsNoTracking()
            .Where(notification => notification.RecipientId == recipientId && !hidden.Contains(notification.ActorId));

        if (after is { } position)
        {
            notifications = notifications.Where(notification => EF.Functions.LessThan(
                ValueTuple.Create(notification.CreatedAt, notification.Id),
                ValueTuple.Create(position.CreatedAt, position.NotificationId)));
        }

        var rows = await (
            from notification in notifications
            join actor in DbContext.Set<User>().AsNoTracking() on notification.ActorId equals actor.Id
            join post in DbContext.Set<Post>().AsNoTracking() on notification.PostId equals (Guid?)post.Id into posts
            from post in posts.DefaultIfEmpty()
            join comment in DbContext.Set<Comment>().AsNoTracking() on notification.CommentId equals (Guid?)comment.Id into comments
            from comment in comments.DefaultIfEmpty()
            orderby notification.CreatedAt descending, notification.Id descending
            select new { Notification = notification, Actor = actor, Post = post, Comment = comment })
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new NotificationEntry(row.Notification, row.Actor, row.Post, row.Comment)).ToList();
    }

    public Task<bool> ExistsAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid postId,
        CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().AnyAsync(
            notification => notification.RecipientId == recipientId
                && notification.ActorId == actorId
                && notification.Type == type
                && notification.PostId == postId,
            cancellationToken);
    }

    public Task<int> CountUnreadForReadAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(recipientId);

        return DbSet.AsNoTracking()
            .CountAsync(
                notification => notification.RecipientId == recipientId
                    && notification.ReadAt == null
                    && users.Any(user => user.Id == notification.ActorId)
                    && !hidden.Contains(notification.ActorId),
                cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await DbSet
            .Where(notification => notification.RecipientId == recipientId && notification.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAt, now),
                cancellationToken);
    }
}
