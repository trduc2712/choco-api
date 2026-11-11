using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Notifications;

public interface INotificationRepository : IRepository<Notification>
{
    Task<Notification?> GetByIdForRecipientAsync(
        Guid id, Guid recipientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationEntry>> ListForReadAsync(
        Guid recipientId,
        (DateTimeOffset CreatedAt, Guid NotificationId)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid postId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadForReadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
