using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.CommentReactions;

public interface ICommentReactionRepository : IRepository<CommentReaction>
{
    Task<bool> AddIfMissingAsync(CommentReaction reaction, CancellationToken cancellationToken);

    Task<CommentReaction?> GetAsync(Guid commentId, Guid userId, CancellationToken cancellationToken);

    Task RemoveIfPresentAsync(Guid commentId, Guid userId, CancellationToken cancellationToken);

    Task RemoveByCommentAsync(Guid commentId, CancellationToken cancellationToken);

    Task RemoveByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CommentReactionSummary>> GetSummariesForReadAsync(
        IReadOnlyCollection<Guid> commentIds, Guid viewerId, CancellationToken cancellationToken);
}
