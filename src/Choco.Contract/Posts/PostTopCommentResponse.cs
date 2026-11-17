namespace Choco.Contract.Posts;

/// <summary>
/// The top-level comment shown under a post: the one whose conversation has the most replies, the newest on a tie.
/// </summary>
/// <param name="Id">The comment's id.</param>
/// <param name="Author">The user who wrote the comment, with the details shown next to it.</param>
/// <param name="Content">The comment text, up to 500 characters.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000003", "author": { "username": "username", "displayName": "Display Name", "avatarUrl": "https://example.com/avatar.png" }, "content": "content" }</example>
public sealed record PostTopCommentResponse(Guid Id, PostAuthorResponse Author, string Content);
