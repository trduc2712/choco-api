using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class UserFollowRepository : Repository<UserFollow>, IUserFollowRepository
{
    public UserFollowRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken)
    {
        await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO user_follows (id, follower_id, followed_user_id, followed_at)
            VALUES ({follow.Id}, {follow.FollowerId}, {follow.FollowedUserId}, {follow.FollowedAt})
            ON CONFLICT (follower_id, followed_user_id) DO NOTHING
            """, cancellationToken);
    }

    public async Task RemoveIfPresentAsync(Guid followerId, Guid followedUserId, CancellationToken cancellationToken)
    {
        await DbSet.Where(follow => follow.FollowerId == followerId && follow.FollowedUserId == followedUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<UserFollowSummary> GetSummaryForReadAsync(
        Guid userId, Guid viewerId, CancellationToken cancellationToken)
    {
        IQueryable<User> users = DbContext.Set<User>();

        bool isFollowedByViewer = await DbSet.AsNoTracking()
            .AnyAsync(follow => follow.FollowerId == viewerId && follow.FollowedUserId == userId, cancellationToken);

        int followerCount = await DbSet.AsNoTracking()
            .CountAsync(
                follow => follow.FollowedUserId == userId && users.Any(user => user.Id == follow.FollowerId),
                cancellationToken);

        int followingCount = await DbSet.AsNoTracking()
            .CountAsync(
                follow => follow.FollowerId == userId && users.Any(user => user.Id == follow.FollowedUserId),
                cancellationToken);

        return new UserFollowSummary(isFollowedByViewer, followerCount, followingCount);
    }
}
