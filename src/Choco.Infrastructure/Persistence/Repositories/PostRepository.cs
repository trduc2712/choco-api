using Choco.Domain.Posts;
using Choco.Domain.Primitives.Entities;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public Task<Post?> GetByIdForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public Task<Post?> GetByIdIncludingDeletedForReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> ListByIdsForReadAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(post => ids.Contains(post.Id)).ToListAsync(cancellationToken);
    }

    public Task<int> CountIncludingDeletedForReadAsync(PostListFilter filter, CancellationToken cancellationToken = default)
    {
        return Filter(filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> ListIncludingDeletedForReadAsync(
        PostListFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await Filter(filter)
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Post> Filter(PostListFilter filter)
    {
        IQueryable<Post> posts = DbSet.IgnoreQueryFilters().AsNoTracking();

        posts = filter.State == ContentState.Deleted
            ? posts.Where(post => post.IsDeleted)
            : posts.Where(post => !post.IsDeleted);

        foreach (string term in filter.Terms)
        {
            string pattern = LikePatterns.Contains(term);
            posts = posts.Where(post => EF.Functions.ILike(
                EF.Functions.Unaccent(post.Content), EF.Functions.Unaccent(pattern), LikePatterns.Escape));
        }

        if (filter.AuthorId is { } authorId)
        {
            posts = posts.Where(post => post.AuthorId == authorId);
        }

        if (filter.From is { } from)
        {
            posts = posts.Where(post => post.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            posts = posts.Where(post => post.CreatedAt < to);
        }

        return posts;
    }

    public Task<int> CountByAuthorForReadAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .CountAsync(post => post.AuthorId == authorId, cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> ListByAuthorForReadAsync(
        Guid authorId,
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);

        IQueryable<Post> posts = DbSet
            .AsNoTracking()
            .Where(post => post.AuthorId == authorId && !hidden.Contains(post.AuthorId));

        if (after is { } position)
        {
            posts = posts.Where(post => EF.Functions.LessThan(
                ValueTuple.Create(post.CreatedAt, post.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await posts
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> ListRecommendedForReadAsync(
        Guid viewerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(viewerId);

        IQueryable<Post> posts = DbSet
            .AsNoTracking()
            .Where(post => post.AuthorId != viewerId
                && users.Any(user => user.Id == post.AuthorId)
                && !hidden.Contains(post.AuthorId));

        if (after is { } position)
        {
            posts = posts.Where(post => EF.Functions.LessThan(
                ValueTuple.Create(post.CreatedAt, post.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await posts
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> ListByFollowedAuthorsForReadAsync(
        Guid followerId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UserFollow> follows = DbContext.Set<UserFollow>();
        IQueryable<User> users = DbContext.Set<User>();
        IQueryable<Guid> hidden = DbContext.Set<UserBlock>().HiddenFrom(followerId);

        IQueryable<Post> posts = DbSet
            .AsNoTracking()
            .Where(post => follows.Any(follow => follow.FollowerId == followerId && follow.FollowedUserId == post.AuthorId)
                && users.Any(user => user.Id == post.AuthorId)
                && !hidden.Contains(post.AuthorId));

        if (after is { } position)
        {
            posts = posts.Where(post => EF.Functions.LessThan(
                ValueTuple.Create(post.CreatedAt, post.Id),
                ValueTuple.Create(position.CreatedAt, position.Id)));
        }

        return await posts
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
