using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

internal static class UserBlockTestData
{
    public static Task AddBlockAsync(ChocoApiFactory factory, User blocker, User blocked, DateTimeOffset? blockedAt = null)
    {
        UserBlock block = UserBlock.Create(blocker.Id, blocked.Id, blockedAt ?? factory.TimeProvider.GetUtcNow()).Value;

        return factory.ExecuteDbContextAsync(db =>
        {
            db.Add(block);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static Task AddFollowAsync(ChocoApiFactory factory, User follower, User followed)
    {
        UserFollow follow = UserFollow.Create(follower.Id, followed.Id, factory.TimeProvider.GetUtcNow()).Value;
        follow.ClearDomainEvents();

        return factory.ExecuteDbContextAsync(db =>
        {
            db.Add(follow);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public static Task<List<UserBlock>> GetBlocksAsync(ChocoApiFactory factory)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<UserBlock>().AsNoTracking()
            .OrderBy(block => block.BlockedAt)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    public static Task<List<UserFollow>> GetFollowsAsync(ChocoApiFactory factory)
    {
        return factory.ExecuteDbContextAsync(db => db.Set<UserFollow>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
