using Choco.Application.Abstractions;
using Choco.Domain.Notifications;
using Choco.Domain.UserFollows;

using MediatR;

namespace Choco.Application.Notifications;

internal sealed class UserFollowedNotificationHandler(NotificationDispatcher dispatcher)
    : INotificationHandler<DomainEventNotification<UserFollowed>>
{
    public Task Handle(DomainEventNotification<UserFollowed> notification, CancellationToken cancellationToken)
    {
        UserFollowed followed = notification.DomainEvent;

        return dispatcher.SendAsync(
            Notification.Create(
                followed.FollowedUserId, followed.FollowerId, NotificationType.Followed, followed.FollowedAt),
            post: null,
            comment: null,
            cancellationToken);
    }
}
