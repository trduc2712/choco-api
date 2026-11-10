using Choco.Application.Abstractions;
using Choco.Application.Abstractions.Notifications;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Choco.Application.Notifications;

internal sealed class UserFollowedNotificationHandler(
    INotificationRepository notificationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    INotificationRealtimePublisher realtimePublisher,
    ILogger<UserFollowedNotificationHandler> logger)
    : INotificationHandler<DomainEventNotification<UserFollowed>>
{
    public async Task Handle(
        DomainEventNotification<UserFollowed> notification, CancellationToken cancellationToken)
    {
        UserFollowed followed = notification.DomainEvent;

        Notification created = Notification.Create(
            followed.FollowedUserId, followed.FollowerId, NotificationType.Followed, followed.FollowedAt);

        notificationRepository.Add(created);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        IReadOnlyList<User> users = await userRepository.ListByIdsForReadAsync(
            [followed.FollowerId, followed.FollowedUserId], cancellationToken);

        User? actor = users.FirstOrDefault(user => user.Id == followed.FollowerId);
        User? recipient = users.FirstOrDefault(user => user.Id == followed.FollowedUserId);

        if (actor is null || recipient is null)
        {
            return;
        }

        NotificationResponse response = NotificationPages.ToResponse(new NotificationEntry(created, actor));

        await unitOfWork.AfterCommitAsync(async afterCommitToken =>
        {
            try
            {
                await realtimePublisher.PublishAsync(recipient.IdentityId, response, afterCommitToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Failed to deliver notification {NotificationId} in real time.", created.Id);
            }
        }, cancellationToken);
    }
}
