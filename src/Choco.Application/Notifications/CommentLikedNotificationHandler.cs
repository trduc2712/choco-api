using Choco.Application.Abstractions;
using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;

using MediatR;

namespace Choco.Application.Notifications;

internal sealed class CommentLikedNotificationHandler(
    ICommentRepository commentRepository,
    IPostRepository postRepository,
    INotificationRepository notificationRepository,
    NotificationDispatcher dispatcher)
    : INotificationHandler<DomainEventNotification<CommentLiked>>
{
    public async Task Handle(DomainEventNotification<CommentLiked> notification, CancellationToken cancellationToken)
    {
        CommentLiked liked = notification.DomainEvent;

        Comment? comment = await commentRepository.GetByIdForReadAsync(liked.CommentId, cancellationToken);
        if (comment is null || comment.AuthorId == liked.LikerId)
        {
            return;
        }

        Post? post = await postRepository.GetByIdForReadAsync(comment.PostId, cancellationToken);
        if (post is null)
        {
            return;
        }

        if (await notificationRepository.ExistsForCommentAsync(
            comment.AuthorId, liked.LikerId, NotificationType.CommentLiked, comment.Id, cancellationToken))
        {
            return;
        }

        await dispatcher.SendAsync(
            Notification.Create(
                comment.AuthorId, liked.LikerId, NotificationType.CommentLiked, liked.LikedAt, post.Id, comment.Id),
            post,
            comment,
            cancellationToken);
    }
}
