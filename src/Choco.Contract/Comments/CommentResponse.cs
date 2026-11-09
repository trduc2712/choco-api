using Choco.Contract.Posts;

namespace Choco.Contract.Comments;

/// <summary>
/// A comment as clients see it.
/// </summary>
/// <param name="Id">The comment's id.</param>
/// <param name="PostId">The id of the post the comment belongs to.</param>
/// <param name="AuthorId">The id of the user who wrote the comment.</param>
/// <param name="Author">The user who wrote the comment, with the details shown next to it.</param>
/// <param name="Content">The comment text, without leading or trailing whitespace.</param>
/// <param name="CreatedAt">When the comment was written, in UTC.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "postId": "00000000-0000-0000-0000-000000000002", "authorId": "00000000-0000-0000-0000-000000000003", "author": { "username": "username", "displayName": "Display Name", "avatarUrl": "https://example.com/avatar.png" }, "content": "content", "createdAt": "2026-01-01T00:00:00+00:00" }</example>
public sealed record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    PostAuthorResponse Author,
    string Content,
    DateTimeOffset CreatedAt);
