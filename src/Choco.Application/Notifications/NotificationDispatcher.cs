using Choco.Application.Abstractions.Notifications;
using Choco.Contract.Notifications;
using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;

using Microsoft.Extensions.Logging;

namespace Choco.Application.Notifications;

internal sealed class NotificationDispatcher(
    INotificationRepository notificationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    INotificationRealtimePublisher realtimePublisher,
    ILogger<NotificationDispatcher> logger)
{
    public async Task SendAsync(Notification notification, Post? post, Comment? comment, CancellationToken cancellationToken)
    {
        notificationRepository.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        IReadOnlyList<User> users = await userRepository.ListByIdsForReadAsync(
            [notification.ActorId, notification.RecipientId], cancellationToken);

        User? actor = users.FirstOrDefault(user => user.Id == notification.ActorId);
        User? recipient = users.FirstOrDefault(user => user.Id == notification.RecipientId);

        if (actor is null || recipient is null)
        {
            return;
        }

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(notification, actor, post, comment));

        await unitOfWork.AfterCommitAsync(async afterCommitToken =>
        {
            try
            {
                await realtimePublisher.PublishAsync(recipient.IdentityId, response, afterCommitToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Failed to deliver notification {NotificationId} in real time.", notification.Id);
            }
        }, cancellationToken);
    }
}
