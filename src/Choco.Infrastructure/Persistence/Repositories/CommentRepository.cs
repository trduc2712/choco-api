using Choco.Domain.Comments;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public Task<Comment?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> ListByPostForReadAsync(
        Guid postId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();

        IQueryable<Comment> comments = DbSet
            .AsNoTracking()
            .Where(comment => comment.PostId == postId && users.Any(user => user.Id == comment.AuthorId));

        if (after is { } position)
        {
            comments = comments.Where(comment => EF.Functions.GreaterThan(
                ValueTuple.Create(comment.CreatedAt, comment.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await comments
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByPostsForReadAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();

        var rows = await DbSet.AsNoTracking()
            .Where(comment => postIds.Contains(comment.PostId) && users.Any(user => user.Id == comment.AuthorId))
            .GroupBy(comment => comment.PostId)
            .Select(group => new { PostId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.PostId, row => row.Count);
    }
}
