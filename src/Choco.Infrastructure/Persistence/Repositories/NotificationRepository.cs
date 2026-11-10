using Choco.Domain.Notifications;
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
        IQueryable<Notification> notifications = DbSet.AsNoTracking()
            .Where(notification => notification.RecipientId == recipientId);

        if (after is { } position)
        {
            notifications = notifications.Where(notification => EF.Functions.LessThan(
                ValueTuple.Create(notification.CreatedAt, notification.Id),
                ValueTuple.Create(position.CreatedAt, position.NotificationId)));
        }

        var rows = await notifications
            .Join(DbContext.Set<User>().AsNoTracking(), notification => notification.ActorId, user => user.Id,
                (notification, user) => new { Notification = notification, Actor = user })
            .OrderByDescending(row => row.Notification.CreatedAt)
            .ThenByDescending(row => row.Notification.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new NotificationEntry(row.Notification, row.Actor)).ToList();
    }

    public Task<int> CountUnreadForReadAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();

        return DbSet.AsNoTracking()
            .CountAsync(
                notification => notification.RecipientId == recipientId
                    && notification.ReadAt == null
                    && users.Any(user => user.Id == notification.ActorId),
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
