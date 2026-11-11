namespace Choco.Contract.Notifications;

/// <summary>A notification for the signed-in user.</summary>
/// <param name="Id">The notification's id, to use when marking it read.</param>
/// <param name="Type">What happened: Followed, PostLiked or PostCommented.</param>
/// <param name="Actor">The user whose action caused the notification.</param>
/// <param name="PostId">The post the notification is about; null for Followed.</param>
/// <param name="PostExcerpt">The beginning of the post's content, shortened to at most 140 characters; null for Followed or when the post was deleted.</param>
/// <param name="CommentId">The comment the notification is about; null unless the type is PostCommented.</param>
/// <param name="CommentExcerpt">The beginning of the comment's content, shortened to at most 140 characters; null unless the type is PostCommented or when the comment was deleted.</param>
/// <param name="CreatedAt">When it happened, in UTC.</param>
/// <param name="IsRead">Whether the user has marked it read.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","type":"PostCommented","actor":{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"},"postId":"00000000-0000-0000-0000-000000000003","postExcerpt":"post content","commentId":"00000000-0000-0000-0000-000000000004","commentExcerpt":"comment content","createdAt":"2026-01-01T00:00:00+00:00","isRead":false}</example>
public sealed record NotificationResponse(
    Guid Id,
    string Type,
    NotificationActorResponse Actor,
    Guid? PostId,
    string? PostExcerpt,
    Guid? CommentId,
    string? CommentExcerpt,
    DateTimeOffset CreatedAt,
    bool IsRead);
