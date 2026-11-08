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

    public Task<IReadOnlyList<User>> ListFollowersForReadAsync(
        Guid userId, Guid? after, int count, CancellationToken cancellationToken)
    {
        IQueryable<Guid> followerIds = DbSet.Where(follow => follow.FollowedUserId == userId)
            .Select(follow => follow.FollowerId);
        return ReadPageAsync(followerIds, after, count, cancellationToken);
    }

    public Task<IReadOnlyList<User>> ListFollowingForReadAsync(
        Guid userId, Guid? after, int count, CancellationToken cancellationToken)
    {
        IQueryable<Guid> followedUserIds = DbSet.Where(follow => follow.FollowerId == userId)
            .Select(follow => follow.FollowedUserId);
        return ReadPageAsync(followedUserIds, after, count, cancellationToken);
    }

    private async Task<IReadOnlyList<User>> ReadPageAsync(
        IQueryable<Guid> relatedUserIds, Guid? after, int count, CancellationToken cancellationToken)
    {
        IQueryable<User> users = DbContext.Set<User>().AsNoTracking()
            .Where(user => relatedUserIds.Contains(user.Id));

        if (after is Guid lastUserId)
        {
            users = users.Where(user => user.Id.CompareTo(lastUserId) > 0);
        }

        return await users.OrderBy(user => user.Id).Take(count).ToListAsync(cancellationToken);
    }
}
