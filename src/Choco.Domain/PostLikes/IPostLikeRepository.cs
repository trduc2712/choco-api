using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.PostLikes;

public interface IPostLikeRepository : IRepository<PostLike>
{
    Task<bool> AddIfMissingAsync(PostLike like, CancellationToken cancellationToken);

    Task RemoveIfPresentAsync(Guid postId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, PostLikeSummary>> GetSummariesForReadAsync(
        IReadOnlyCollection<Guid> postIds, Guid viewerId, CancellationToken cancellationToken);
}
