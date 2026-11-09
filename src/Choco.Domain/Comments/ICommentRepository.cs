using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Comments;

public interface ICommentRepository : IRepository<Comment>
{
    Task<IReadOnlyList<Comment>> ListByPostForReadAsync(
        Guid postId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountByPostsForReadAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default);
}
