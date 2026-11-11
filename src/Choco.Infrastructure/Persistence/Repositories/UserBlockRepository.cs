using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class UserBlockRepository : Repository<UserBlock>, IUserBlockRepository
{
    public UserBlockRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task<bool> AddIfMissingAsync(UserBlock block, CancellationToken cancellationToken)
    {
        int inserted = await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO user_blocks (id, blocker_id, blocked_user_id, blocked_at)
            VALUES ({block.Id}, {block.BlockerId}, {block.BlockedUserId}, {block.BlockedAt})
            ON CONFLICT (blocker_id, blocked_user_id) DO NOTHING
            """, cancellationToken);

        if (inserted == 0)
        {
            return false;
        }

        DbSet.Attach(block);
        return true;
    }

    public async Task RemoveIfPresentAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken)
    {
        await DbSet.Where(block => block.BlockerId == blockerId && block.BlockedUserId == blockedUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> ExistsForReadAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken)
    {
        return DbSet.AsNoTracking()
            .AnyAsync(block => block.BlockerId == blockerId && block.BlockedUserId == blockedUserId, cancellationToken);
    }

    public Task<bool> ExistsBetweenForReadAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        return DbSet.AsNoTracking().AnyAsync(
            block => (block.BlockerId == userId && block.BlockedUserId == otherUserId)
                || (block.BlockerId == otherUserId && block.BlockedUserId == userId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<UserBlockEntry>> ListBlockedForReadAsync(
        Guid blockerId,
        (DateTimeOffset BlockedAt, Guid BlockId)? after,
        int count,
        CancellationToken cancellationToken)
    {
        IQueryable<UserBlock> blocks = DbSet.AsNoTracking().Where(block => block.BlockerId == blockerId);

        if (after is { } position)
        {
            blocks = blocks.Where(block => EF.Functions.LessThan(
                ValueTuple.Create(block.BlockedAt, block.Id),
                ValueTuple.Create(position.BlockedAt, position.BlockId)));
        }

        var rows = await blocks
            .Join(DbContext.Set<User>().AsNoTracking(), block => block.BlockedUserId, user => user.Id,
                (block, user) => new { BlockId = block.Id, block.BlockedAt, User = user })
            .OrderByDescending(row => row.BlockedAt)
            .ThenByDescending(row => row.BlockId)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new UserBlockEntry(row.BlockId, row.BlockedAt, row.User)).ToList();
    }
}
