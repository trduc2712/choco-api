using Choco.Contract.Abstractions;

namespace Choco.Contract.Comments;

/// <summary>
/// A comment as an administrator sees it, deleted or not.
/// </summary>
/// <param name="Id">The comment's id.</param>
/// <param name="PostId">The id of the post the comment belongs to, even when that post is deleted.</param>
/// <param name="Content">The comment text.</param>
/// <param name="Author">The user who wrote the comment.</param>
/// <param name="CreatedAt">When the comment was written, in UTC.</param>
/// <param name="IsDeleted">Whether the comment was deleted.</param>
/// <param name="DeletedAt">When the comment was deleted, in UTC; null when it is not deleted.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","postId":"00000000-0000-0000-0000-000000000003","content":"content","author":{"id":"00000000-0000-0000-0000-000000000002","username":"username"},"createdAt":"2026-01-01T00:00:00+00:00","isDeleted":false,"deletedAt":null}</example>
public sealed record AdminCommentResponse(
    Guid Id,
    Guid PostId,
    string Content,
    ContentAuthorResponse Author,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt);
