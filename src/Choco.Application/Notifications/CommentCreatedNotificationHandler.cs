using Choco.Application.Abstractions;
using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;

using MediatR;

namespace Choco.Application.Notifications;

internal sealed class CommentCreatedNotificationHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    NotificationDispatcher dispatcher,
    TimeProvider timeProvider)
    : INotificationHandler<DomainEventNotification<CommentCreated>>
{
    public async Task Handle(DomainEventNotification<CommentCreated> notification, CancellationToken cancellationToken)
    {
        CommentCreated created = notification.DomainEvent;

        Post? post = await postRepository.GetByIdForReadAsync(created.PostId, cancellationToken);
        if (post is null || post.AuthorId == created.AuthorId)
        {
            return;
        }

        Comment? comment = await commentRepository.GetByIdForReadAsync(created.CommentId, cancellationToken);
        if (comment is null)
        {
            return;
        }

        await dispatcher.SendAsync(
            Notification.Create(
                post.AuthorId,
                created.AuthorId,
                NotificationType.PostCommented,
                timeProvider.GetUtcNow(),
                post.Id,
                comment.Id),
            post,
            comment,
            cancellationToken);
    }
}
