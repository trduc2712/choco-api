using Choco.Application.Abstractions;
using Choco.Domain.Notifications;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;

using MediatR;

namespace Choco.Application.Notifications;

internal sealed class PostLikedNotificationHandler(
    IPostRepository postRepository,
    INotificationRepository notificationRepository,
    NotificationDispatcher dispatcher)
    : INotificationHandler<DomainEventNotification<PostLiked>>
{
    public async Task Handle(DomainEventNotification<PostLiked> notification, CancellationToken cancellationToken)
    {
        PostLiked liked = notification.DomainEvent;

        Post? post = await postRepository.GetByIdForReadAsync(liked.PostId, cancellationToken);
        if (post is null || post.AuthorId == liked.LikerId)
        {
            return;
        }

        if (await notificationRepository.ExistsAsync(
            post.AuthorId, liked.LikerId, NotificationType.PostLiked, post.Id, cancellationToken))
        {
            return;
        }

        await dispatcher.SendAsync(
            Notification.Create(post.AuthorId, liked.LikerId, NotificationType.PostLiked, liked.LikedAt, post.Id),
            post,
            comment: null,
            cancellationToken);
    }
}
