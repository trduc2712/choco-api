using Choco.Contract.Abstractions;

namespace Choco.Contract.Posts;

/// <summary>
/// A post as an administrator sees it, deleted or not.
/// </summary>
/// <param name="Id">The post's id.</param>
/// <param name="Content">The post text.</param>
/// <param name="Author">The user who wrote the post.</param>
/// <param name="CreatedAt">When the post was published, in UTC.</param>
/// <param name="IsDeleted">Whether the post was deleted.</param>
/// <param name="DeletedAt">When the post was deleted, in UTC; null when it is not deleted.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","content":"content","author":{"id":"00000000-0000-0000-0000-000000000002","username":"username"},"createdAt":"2026-01-01T00:00:00+00:00","isDeleted":false,"deletedAt":null}</example>
public sealed record AdminPostResponse(
    Guid Id,
    string Content,
    ContentAuthorResponse Author,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt);
