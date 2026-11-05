using Choco.Domain.Posts;
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

    private Task DeletePostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(post => post.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<IReadOnlyList<Post>> ListAsync(Guid authorId, (DateTimeOffset CreatedAt, Guid Id)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPostRepository repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();

        return await repository.ListByAuthorForReadAsync(authorId, after, count, TestContext.Current.CancellationToken);
    }
}
