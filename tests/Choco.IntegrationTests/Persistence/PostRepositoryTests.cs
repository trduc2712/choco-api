using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class PostRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public PostRepositoryTests(ChocoApiFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ReturnsAuthorsPostsNewestFirst()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post first = await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        Post third = await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListAsync(author.Id, null, 10);

        posts.Select(post => post.Id).Should().Equal(third.Id, second.Id, first.Id);
    }

    [Fact]
    public async Task SkipsOtherAuthorsAndDeletedPosts()
    {
        User author = await AddUserAsync("identity-id-1", "username-1");
        User otherAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post kept = await AddPostAsync(author, "content-1");
        Post deleted = await AddPostAsync(author, "content-2");
        await AddPostAsync(otherAuthor, "content-3");
        await DeletePostAsync(deleted.Id);

        IReadOnlyList<Post> posts = await ListAsync(author.Id, null, 10);

        posts.Select(post => post.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task WithPosition_ReturnsOnlyOlderPosts()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post first = await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListAsync(author.Id, (second.CreatedAt, second.Id), 10);

        posts.Select(post => post.Id).Should().Equal(first.Id);
    }

    [Fact]
    public async Task PostsWithSameCreatedAt_AreOrderedByIdAndPagedWithoutGaps()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post one = Post.Create("content-1", author.Id);
        Post two = Post.Create("content-2", author.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(one, two);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Guid[] expectedOrder = new[] { one.Id, two.Id }
            .OrderByDescending(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<Post> firstPage = await ListAsync(author.Id, null, 1);

        IReadOnlyList<Post> secondPage = await ListAsync(author.Id, (firstPage[0].CreatedAt, firstPage[0].Id), 1);

        firstPage.Select(post => post.Id).Should().Equal(expectedOrder[0]);
        secondPage.Select(post => post.Id).Should().Equal(expectedOrder[1]);
    }

    [Fact]
    public async Task ReturnsAtMostCount()
    {
        User author = await AddUserAsync("identity-id", "username");
        await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        Post third = await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListAsync(author.Id, null, 2);

        posts.Select(post => post.Id).Should().Equal(third.Id, second.Id);
    }

    [Fact]
    public async Task Count_ReturnsTheAuthorsPostsAndSkipsDeletedAndOthersPosts()
    {
        User author = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        await AddPostAsync(author, "content-1");
        await AddPostAsync(author, "content-2");
        Post deleted = await AddPostAsync(author, "content-3");
        await AddPostAsync(other, "content-4");
        await DeletePostAsync(deleted.Id);

        int count = await CountAsync(author.Id);

        count.Should().Be(2);
    }

    [Fact]
    public async Task Count_WithoutPosts_ReturnsZero()
    {
        User author = await AddUserAsync("identity-id", "username");

        int count = await CountAsync(author.Id);

        count.Should().Be(0);
    }

    [Fact]
    public async Task Recommended_ReturnsOthersPostsNewestFirst()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User followedAuthor = await AddUserAsync("identity-id-2", "username-2");
        User otherAuthor = await AddUserAsync("identity-id-3", "username-3");
        User deletedAuthor = await AddUserAsync("identity-id-4", "username-4");
        await FollowAsync(viewer, followedAuthor);
        Post first = await AddPostAsync(followedAuthor, "content-1");
        Post deleted = await AddPostAsync(otherAuthor, "content-2");
        Post second = await AddPostAsync(otherAuthor, "content-3");
        await AddPostAsync(viewer, "content-4");
        await AddPostAsync(deletedAuthor, "content-5");
        await DeletePostAsync(deleted.Id);
        await DeleteUserAsync(deletedAuthor.Id);

        IReadOnlyList<Post> posts = await ListRecommendedAsync(viewer.Id, null, 10);

        posts.Select(post => post.Id).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task Recommended_WithOnlyOwnPosts_ReturnsNothing()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        await AddPostAsync(viewer, "content-1");

        IReadOnlyList<Post> posts = await ListRecommendedAsync(viewer.Id, null, 10);

        posts.Should().BeEmpty();
    }

    [Fact]
    public async Task Recommended_WithPosition_ReturnsOnlyOlderPosts()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post first = await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListRecommendedAsync(viewer.Id, (second.CreatedAt, second.Id), 10);

        posts.Select(post => post.Id).Should().Equal(first.Id);
    }

    [Fact]
    public async Task Recommended_ReturnsAtMostCount()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        Post third = await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListRecommendedAsync(viewer.Id, null, 2);

        posts.Select(post => post.Id).Should().Equal(third.Id, second.Id);
    }

    [Fact]
    public async Task Recommended_WithSameCreatedAt_OrdersByIdAndPagesWithoutGaps()
    {
        User viewer = await AddUserAsync("identity-id", "username");
        User firstAuthor = await AddUserAsync("identity-id-2", "username-2");
        User secondAuthor = await AddUserAsync("identity-id-3", "username-3");
        Post one = Post.Create("content-1", firstAuthor.Id);
        Post two = Post.Create("content-2", secondAuthor.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(one, two);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Guid[] expectedOrder = new[] { one.Id, two.Id }
            .OrderByDescending(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<Post> firstPage = await ListRecommendedAsync(viewer.Id, null, 1);

        IReadOnlyList<Post> secondPage = await ListRecommendedAsync(viewer.Id, (firstPage[0].CreatedAt, firstPage[0].Id), 1);

        firstPage.Select(post => post.Id).Should().Equal(expectedOrder[0]);
        secondPage.Select(post => post.Id).Should().Equal(expectedOrder[1]);
    }

    [Fact]
    public async Task FollowedAuthors_ReturnsOnlyFollowedAuthorsPostsNewestFirst()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User firstAuthor = await AddUserAsync("identity-id-2", "username-2");
        User secondAuthor = await AddUserAsync("identity-id-3", "username-3");
        User notFollowed = await AddUserAsync("identity-id-4", "username-4");
        User deletedAuthor = await AddUserAsync("identity-id-5", "username-5");
        await FollowAsync(follower, firstAuthor, secondAuthor, deletedAuthor);
        await FollowAsync(notFollowed, follower);
        Post first = await AddPostAsync(firstAuthor, "content-1");
        Post deleted = await AddPostAsync(firstAuthor, "content-2");
        Post second = await AddPostAsync(secondAuthor, "content-3");
        await AddPostAsync(notFollowed, "content-4");
        await AddPostAsync(follower, "content-5");
        await AddPostAsync(deletedAuthor, "content-6");
        await DeletePostAsync(deleted.Id);
        await DeleteUserAsync(deletedAuthor.Id);

        IReadOnlyList<Post> posts = await ListFollowedAsync(follower.Id, null, 10);

        posts.Select(post => post.Id).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task FollowedAuthors_WithoutFollows_ReturnsNothing()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddPostAsync(author, "content-1");

        IReadOnlyList<Post> posts = await ListFollowedAsync(follower.Id, null, 10);

        posts.Should().BeEmpty();
    }

    [Fact]
    public async Task FollowedAuthors_WithPosition_ReturnsOnlyOlderPosts()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        await FollowAsync(follower, author);
        Post first = await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListFollowedAsync(follower.Id, (second.CreatedAt, second.Id), 10);

        posts.Select(post => post.Id).Should().Equal(first.Id);
    }

    [Fact]
    public async Task FollowedAuthors_ReturnsAtMostCount()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        await FollowAsync(follower, author);
        await AddPostAsync(author, "content-1");
        Post second = await AddPostAsync(author, "content-2");
        Post third = await AddPostAsync(author, "content-3");

        IReadOnlyList<Post> posts = await ListFollowedAsync(follower.Id, null, 2);

        posts.Select(post => post.Id).Should().Equal(third.Id, second.Id);
    }

    [Fact]
    public async Task FollowedAuthors_WithSameCreatedAt_OrdersByIdAndPagesWithoutGaps()
    {
        User follower = await AddUserAsync("identity-id", "username");
        User firstAuthor = await AddUserAsync("identity-id-2", "username-2");
        User secondAuthor = await AddUserAsync("identity-id-3", "username-3");
        await FollowAsync(follower, firstAuthor, secondAuthor);
        Post one = Post.Create("content-1", firstAuthor.Id);
        Post two = Post.Create("content-2", secondAuthor.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(one, two);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Guid[] expectedOrder = new[] { one.Id, two.Id }
            .OrderByDescending(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<Post> firstPage = await ListFollowedAsync(follower.Id, null, 1);

        IReadOnlyList<Post> secondPage = await ListFollowedAsync(follower.Id, (firstPage[0].CreatedAt, firstPage[0].Id), 1);

        firstPage.Select(post => post.Id).Should().Equal(expectedOrder[0]);
        secondPage.Select(post => post.Id).Should().Equal(expectedOrder[1]);
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<Post> AddPostAsync(User author, string content)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Post post = Post.Create(content, author.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private async Task<int> CountAsync(Guid authorId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostRepository repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();

        return await repository.CountByAuthorForReadAsync(authorId, TestContext.Current.CancellationToken);
    }

    private Task DeletePostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(post => post.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task FollowAsync(User follower, params User[] followed)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            dbContext.AddRange(followed.Select(user => UserFollow.Create(follower.Id, user.Id, now).Value));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<IReadOnlyList<Post>> ListRecommendedAsync(Guid viewerId, (DateTimeOffset CreatedAt, Guid Id)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostRepository repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();

        return await repository.ListRecommendedForReadAsync(viewerId, after, count, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<Post>> ListFollowedAsync(Guid followerId, (DateTimeOffset CreatedAt, Guid Id)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostRepository repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();

        return await repository.ListByFollowedAuthorsForReadAsync(followerId, after, count, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<Post>> ListAsync(Guid authorId, (DateTimeOffset CreatedAt, Guid Id)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostRepository repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();

        return await repository.ListByAuthorForReadAsync(authorId, new Guid("00000000-0000-0000-0000-000000000099"), after, count, TestContext.Current.CancellationToken);
    }
}
