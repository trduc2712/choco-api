using Choco.Domain.CommentReactions;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class CommentReactionRepository : Repository<CommentReaction>, ICommentReactionRepository
{
    public CommentReactionRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task<bool> AddIfMissingAsync(CommentReaction reaction, CancellationToken cancellationToken)
    {
        int inserted = await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO comment_reactions (id, comment_id, user_id, type, reacted_at)
            VALUES ({reaction.Id}, {reaction.CommentId}, {reaction.UserId}, {reaction.Type.ToString()}, {reaction.ReactedAt})
            ON CONFLICT (comment_id, user_id) DO NOTHING
            """, cancellationToken);

        if (inserted == 0)
        {
            return false;
        }

        DbSet.Attach(reaction);
        return true;
    }

    public Task<CommentReaction?> GetAsync(Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        return DbSet.FirstOrDefaultAsync(
            reaction => reaction.CommentId == commentId && reaction.UserId == userId, cancellationToken);
    }

    public async Task RemoveIfPresentAsync(Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        await DbSet.Where(reaction => reaction.CommentId == commentId && reaction.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RemoveByCommentAsync(Guid commentId, CancellationToken cancellationToken)
    {
        await DbSet.Where(reaction => reaction.CommentId == commentId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RemoveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await DbSet.Where(reaction => reaction.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CommentReactionSummary>> GetSummariesForReadAsync(
        IReadOnlyCollection<Guid> commentIds, Guid viewerId, CancellationToken cancellationToken)
    {
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);

        var rows = await DbSet.AsNoTracking()
            .Where(reaction => commentIds.Contains(reaction.CommentId)
                && users.Any(user => user.Id == reaction.UserId)
                && !hidden.Contains(reaction.UserId))
            .GroupBy(reaction => reaction.CommentId)
            .Select(group => new
            {
                CommentId = group.Key,
                LikeCount = group.Count(reaction => reaction.Type == CommentReactionType.Like),
                DislikeCount = group.Count(reaction => reaction.Type == CommentReactionType.Dislike),
                ViewerReaction = group.Where(reaction => reaction.UserId == viewerId)
                    .Select(reaction => (CommentReactionType?)reaction.Type)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.CommentId,
            row => new CommentReactionSummary(row.LikeCount, row.DislikeCount, row.ViewerReaction));
    }
}
