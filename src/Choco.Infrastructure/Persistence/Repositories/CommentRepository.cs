using Choco.Domain.Comments;
using Choco.Domain.Primitives.Entities;
using Choco.Domain.UserBlocks;
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

    public Task<Comment?> GetByIdIncludingDeletedForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public Task<int> CountIncludingDeletedForReadAsync(CommentListFilter filter, CancellationToken cancellationToken = default)
    {
        return Filter(filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> ListIncludingDeletedForReadAsync(
        CommentListFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await Filter(filter)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Comment> Filter(CommentListFilter filter)
    {
        IQueryable<Comment> comments = DbSet.IgnoreQueryFilters().AsNoTracking();

        comments = filter.State == ContentState.Deleted
            ? comments.Where(comment => comment.IsDeleted)
            : comments.Where(comment => !comment.IsDeleted);

        foreach (string term in filter.Terms)
        {
            string pattern = LikePatterns.Contains(term);
            comments = comments.Where(comment => EF.Functions.ILike(
                EF.Functions.Unaccent(comment.Content), EF.Functions.Unaccent(pattern), LikePatterns.Escape));
        }

        if (filter.AuthorId is { } authorId)
        {
            comments = comments.Where(comment => comment.AuthorId == authorId);
        }

        if (filter.PostId is { } postId)
        {
            comments = comments.Where(comment => comment.PostId == postId);
        }

        if (filter.From is { } from)
        {
            comments = comments.Where(comment => comment.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            comments = comments.Where(comment => comment.CreatedAt < to);
        }

        return comments;
    }

    public async Task<IReadOnlyList<Comment>> ListByPostForReadAsync(
        Guid postId,
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);

        IQueryable<Comment> comments = DbSet
            .AsNoTracking()
            .Where(comment => comment.PostId == postId
                && users.Any(user => user.Id == comment.AuthorId)
                && !hidden.Contains(comment.AuthorId));

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
