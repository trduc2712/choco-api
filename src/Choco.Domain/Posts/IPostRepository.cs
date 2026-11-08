using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Posts;

public interface IPostRepository : IRepository<Post>
{
    Task<Post?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> ListByAuthorForReadAsync(
        Guid authorId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> ListRecommendedForReadAsync(
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> ListByFollowedAuthorsForReadAsync(
        Guid followerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default);
}
