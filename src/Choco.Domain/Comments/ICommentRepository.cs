using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Comments;

public interface ICommentRepository : IRepository<Comment>
{
    Task<Comment?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Comment?> GetByIdIncludingDeletedForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountIncludingDeletedForReadAsync(CommentListFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> ListIncludingDeletedForReadAsync(
        CommentListFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> ListByPostForReadAsync(
        Guid postId,
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountByPostsForReadAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default);
}
