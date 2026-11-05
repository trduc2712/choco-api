using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Posts;

public interface IPostRepository : IRepository<Post>
{
    Task<Post?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);
}
