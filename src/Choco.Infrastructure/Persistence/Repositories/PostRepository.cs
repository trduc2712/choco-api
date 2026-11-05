using Choco.Domain.Posts;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public Task<Post?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }
}
