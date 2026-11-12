using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Comments;

public sealed record CommentListFilter(
    IReadOnlyList<string> Terms,
    Guid? AuthorId,
    Guid? PostId,
    ContentState State,
    DateTimeOffset? From,
    DateTimeOffset? To);
