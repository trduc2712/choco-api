namespace Choco.Contract.Posts;

/// <summary>
/// A post as clients see it.
/// </summary>
/// <param name="Id">The post's id.</param>
/// <param name="AuthorId">The id of the user who wrote the post.</param>
/// <param name="Content">The post text, without leading or trailing whitespace.</param>
/// <param name="CreatedAt">When the post was published, in UTC.</param>
/// <param name="ModifiedAt">When the post was last changed, in UTC; equals CreatedAt until the post is edited.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "authorId": "00000000-0000-0000-0000-000000000002", "content": "content", "createdAt": "2026-01-01T00:00:00+00:00", "modifiedAt": "2026-01-01T00:00:00+00:00" }</example>
public sealed record PostResponse(
    Guid Id,
    Guid AuthorId,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);
