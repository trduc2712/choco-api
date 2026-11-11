using Choco.Domain.Primitives.Events;

namespace Choco.Domain.Comments;

public sealed record CommentCreated(Guid CommentId, Guid PostId, Guid AuthorId) : IDomainEvent;
