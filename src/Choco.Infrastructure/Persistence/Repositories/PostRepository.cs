using Choco.Domain.Posts;
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

    public async Task<IReadOnlyList<Post>> ListByAuthorForReadAsync(
        Guid authorId,
        (DateTimeOffset CreatedAt, Guid Id)? after,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Post> posts = DbSet
            .AsNoTracking()
            .Where(post => post.AuthorId == authorId);

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

        IQueryable<Post> posts = DbSet
            .AsNoTracking()
            .Where(post => follows.Any(follow => follow.FollowerId == followerId && follow.FollowedUserId == post.AuthorId)
                && users.Any(user => user.Id == post.AuthorId));

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
