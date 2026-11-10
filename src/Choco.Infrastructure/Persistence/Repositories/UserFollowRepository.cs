using System.Linq.Expressions;

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

    public async Task<IReadOnlySet<Guid>> GetFollowedUserIdsForReadAsync(
        Guid followerId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        List<Guid> followedUserIds = await DbSet.AsNoTracking()
            .Where(follow => follow.FollowerId == followerId && userIds.Contains(follow.FollowedUserId))
            .Select(follow => follow.FollowedUserId)
            .ToListAsync(cancellationToken);

        return followedUserIds.ToHashSet();
    }

    public Task<IReadOnlyList<UserFollowEntry>> ListFollowersForReadAsync(
        Guid userId,
        (DateTimeOffset FollowedAt, Guid FollowId)? after,
        int count,
        CancellationToken cancellationToken)
    {
        return ListAsync(
            follow => follow.FollowedUserId == userId, follow => follow.FollowerId, after, count, cancellationToken);
    }

    public Task<IReadOnlyList<UserFollowEntry>> ListFollowingForReadAsync(
        Guid userId,
        (DateTimeOffset FollowedAt, Guid FollowId)? after,
        int count,
        CancellationToken cancellationToken)
    {
        return ListAsync(
            follow => follow.FollowerId == userId, follow => follow.FollowedUserId, after, count, cancellationToken);
    }

    private async Task<IReadOnlyList<UserFollowEntry>> ListAsync(
        Expression<Func<UserFollow, bool>> owner,
        Expression<Func<UserFollow, Guid>> listedUserId,
        (DateTimeOffset FollowedAt, Guid FollowId)? after,
        int count,
        CancellationToken cancellationToken)
    {
        IQueryable<UserFollow> follows = DbSet.AsNoTracking().Where(owner);

        if (after is { } position)
        {
            follows = follows.Where(follow => EF.Functions.LessThan(
                ValueTuple.Create(follow.FollowedAt, follow.Id),
                ValueTuple.Create(position.FollowedAt, position.FollowId)));
        }

        var rows = await follows
            .Join(DbContext.Set<User>().AsNoTracking(), listedUserId, user => user.Id,
                (follow, user) => new { FollowId = follow.Id, follow.FollowedAt, User = user })
            .OrderByDescending(row => row.FollowedAt)
            .ThenByDescending(row => row.FollowId)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new UserFollowEntry(row.FollowId, row.FollowedAt, row.User)).ToList();
    }
}
