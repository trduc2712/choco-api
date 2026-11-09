using Choco.Domain.PostLikes;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class PostLikeRepository : Repository<PostLike>, IPostLikeRepository
{
    public PostLikeRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task AddIfMissingAsync(PostLike like, CancellationToken cancellationToken)
    {
        await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO post_likes (id, post_id, user_id, liked_at)
            VALUES ({like.Id}, {like.PostId}, {like.UserId}, {like.LikedAt})
            ON CONFLICT (post_id, user_id) DO NOTHING
            """, cancellationToken);
    }

    public async Task RemoveIfPresentAsync(Guid postId, Guid userId, CancellationToken cancellationToken)
    {
        await DbSet.Where(like => like.PostId == postId && like.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, PostLikeSummary>> GetSummariesForReadAsync(
        IReadOnlyCollection<Guid> postIds, Guid viewerId, CancellationToken cancellationToken)
    {
        IQueryable<User> users = DbContext.Set<User>();

        var rows = await DbSet.AsNoTracking()
            .Where(like => postIds.Contains(like.PostId) && users.Any(user => user.Id == like.UserId))
            .GroupBy(like => like.PostId)
            .Select(group => new
            {
                PostId = group.Key,
                Count = group.Count(),
                LikedByViewer = group.Any(like => like.UserId == viewerId),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.PostId, row => new PostLikeSummary(row.Count, row.LikedByViewer));
    }
}
