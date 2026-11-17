using Choco.Contract.Posts;

namespace Choco.Contract.Comments;

/// <summary>
/// A comment as clients see it. A comment the caller can't see but that has replies is a placeholder: isDeleted is true, and authorId, author and content are null.
/// </summary>
/// <param name="Id">The comment's id.</param>
/// <param name="PostId">The id of the post the comment belongs to.</param>
/// <param name="ParentId">The id of the comment this one replies to; null for a comment written on the post itself.</param>
/// <param name="AuthorId">The id of the user who wrote the comment; null for a placeholder.</param>
/// <param name="Author">The user who wrote the comment, with the details shown next to it; null for a placeholder.</param>
/// <param name="Content">The comment text, without leading or trailing whitespace; null for a placeholder.</param>
/// <param name="ReplyCount">How many direct replies the caller sees when listing this comment's replies, placeholders included.</param>
/// <param name="LikeCount">How many users like the comment, leaving out deleted accounts and users who blocked the caller or whom the caller blocked; 0 for a placeholder.</param>
/// <param name="DislikeCount">How many users dislike the comment, counted the same way as likeCount; 0 for a placeholder.</param>
/// <param name="MyReaction">The caller's reaction to the comment: like, dislike, or null when the caller has none or the comment is a placeholder.</param>
/// <param name="IsDeleted">Whether the comment is a placeholder: it was deleted, its author's account was deleted, or its author and the caller blocked each other.</param>
/// <param name="CreatedAt">When the comment was written, in UTC.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "postId": "00000000-0000-0000-0000-000000000002", "parentId": "00000000-0000-0000-0000-000000000004", "authorId": "00000000-0000-0000-0000-000000000003", "author": { "username": "username", "displayName": "Display Name", "avatarUrl": "https://example.com/avatar.png" }, "content": "content", "replyCount": 0, "likeCount": 2, "dislikeCount": 1, "myReaction": "like", "isDeleted": false, "createdAt": "2026-01-01T00:00:00+00:00" }</example>
public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid? ParentId,
    Guid? AuthorId,
    PostAuthorResponse? Author,
    string? Content,
    int ReplyCount,
    int LikeCount,
    int DislikeCount,
    string? MyReaction,
    bool IsDeleted,
    DateTimeOffset CreatedAt);
