using Choco.Domain.UserBlocks;

namespace Choco.Infrastructure.Persistence.Repositories;

internal static class UserBlockQueries
{
    public static IQueryable<Guid> HiddenFrom(this IQueryable<UserBlock> blocks, Guid viewerId)
    {
        return blocks.Where(block => block.BlockerId == viewerId).Select(block => block.BlockedUserId)
            .Union(blocks.Where(block => block.BlockedUserId == viewerId).Select(block => block.BlockerId));
    }
}
