using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Posts;

public sealed record PostListFilter(
    IReadOnlyList<string> Terms,
    Guid? AuthorId,
    ContentState State,
    DateTimeOffset? From,
    DateTimeOffset? To);
