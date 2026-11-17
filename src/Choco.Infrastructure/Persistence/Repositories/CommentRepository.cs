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

    public async Task<IReadOnlyList<CommentThreadEntry>> ListThreadLevelForReadAsync(
        Guid postId,
        Guid? parentId,
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>().Where(user => !user.IsDeleted);
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);
        IQueryable<Comment> all = DbSet.IgnoreQueryFilters();

        IQueryable<Comment> level = all.AsNoTracking().Where(comment => comment.PostId == postId);

        level = parentId is { } id
            ? level.Where(comment => comment.ParentId == id)
            : level.Where(comment => comment.ParentId == null);

        level = level.Where(comment =>
            (!comment.IsDeleted && users.Any(user => user.Id == comment.AuthorId) && !hidden.Contains(comment.AuthorId))
            || all.Any(child => child.ParentId == comment.Id));

        if (after is { } position)
        {
            level = level.Where(comment => EF.Functions.GreaterThan(
                ValueTuple.Create(comment.CreatedAt, comment.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        var rows = await level
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Take(count)
            .Select(comment => new
            {
                Comment = comment,
                IsVisible = !comment.IsDeleted
                    && users.Any(user => user.Id == comment.AuthorId)
                    && !hidden.Contains(comment.AuthorId),
                ReplyCount = all.Count(child => child.ParentId == comment.Id
                    && ((!child.IsDeleted && users.Any(user => user.Id == child.AuthorId) && !hidden.Contains(child.AuthorId))
                        || all.Any(grandchild => grandchild.ParentId == child.Id)))
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CommentThreadEntry(row.Comment, row.IsVisible, row.ReplyCount)).ToList();
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

    public async Task<IReadOnlyDictionary<Guid, Comment>> GetTopRootsByPostsForReadAsync(
        IReadOnlyCollection<Guid> postIds, Guid viewerId, CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<User> activeUsers = users.Where(user => user.SuspendedAt == null);
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);

        List<Comment> tops = await DbSet.AsNoTracking()
            .Where(comment => postIds.Contains(comment.PostId)
                && comment.ParentId == null
                && activeUsers.Any(user => user.Id == comment.AuthorId)
                && !hidden.Contains(comment.AuthorId))
            .Select(comment => new
            {
                Comment = comment,
                ReplyCount = DbSet.Count(reply => reply.RootId == comment.Id
                    && users.Any(user => user.Id == reply.AuthorId))
            })
            .GroupBy(row => row.Comment.PostId)
            .Select(group => group
                .OrderByDescending(row => row.ReplyCount)
                .ThenByDescending(row => row.Comment.CreatedAt)
                .ThenByDescending(row => row.Comment.Id)
                .Select(row => row.Comment)
                .First())
            .ToListAsync(cancellationToken);

        return tops.ToDictionary(comment => comment.PostId);
    }
}
