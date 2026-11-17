namespace Choco.Contract.Posts;

/// <summary>
/// A post as clients see it.
/// </summary>
/// <param name="Id">The post's id.</param>
/// <param name="AuthorId">The id of the user who wrote the post.</param>
/// <param name="Author">The user who wrote the post, with the details shown next to it.</param>
/// <param name="Content">The post text, without leading or trailing whitespace.</param>
/// <param name="LikeCount">How many users like the post, zero when nobody does.</param>
/// <param name="IsLikedByMe">Whether the caller likes the post.</param>
/// <param name="CommentCount">How many comments the post has, zero when there are none.</param>
/// <param name="CreatedAt">When the post was published, in UTC.</param>
/// <param name="ModifiedAt">When the post was last changed, in UTC; equals CreatedAt until the post is edited.</param>
/// <param name="TopComment">The top-level comment with the most replies in its conversation, the newest on a tie, among those the caller may see; null when there is none.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "authorId": "00000000-0000-0000-0000-000000000002", "author": { "username": "username", "displayName": "Display Name", "avatarUrl": "https://example.com/avatar.png" }, "content": "content", "likeCount": 3, "isLikedByMe": true, "commentCount": 2, "createdAt": "2026-01-01T00:00:00+00:00", "modifiedAt": "2026-01-01T00:00:00+00:00", "topComment": { "id": "00000000-0000-0000-0000-000000000003", "author": { "username": "username-2", "displayName": "Display Name 2", "avatarUrl": "https://example.com/avatar-2.png" }, "content": "content" } }</example>
public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    PostAuthorResponse Author,
    string Content,
    int LikeCount,
    bool IsLikedByMe,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    PostTopCommentResponse? TopComment);
