using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class CommentRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public CommentRepositoryTests(ChocoApiFactory factory)
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
    public async Task ReturnsThePostsCommentsOldestFirst()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        Comment first = await AddCommentAsync(post, author, "content-1");
        Comment second = await AddCommentAsync(post, author, "content-2");
        Comment third = await AddCommentAsync(post, author, "content-3");

        IReadOnlyList<Comment> comments = await ListAsync(post.Id, null, 10);

        comments.Select(comment => comment.Id).Should().Equal(first.Id, second.Id, third.Id);
    }

    [Fact]
    public async Task SkipsOtherPostsDeletedCommentsAndDeletedAuthors()
    {
        User author = await AddUserAsync("identity-id-1", "username-1");
        User deletedAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Post otherPost = await AddPostAsync(author);
        Comment kept = await AddCommentAsync(post, author, "content-1");
        Comment deleted = await AddCommentAsync(post, author, "content-2");
        await AddCommentAsync(post, deletedAuthor, "content-3");
        await AddCommentAsync(otherPost, author, "content-4");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Comment>().Single(comment => comment.Id == deleted.Id));
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == deletedAuthor.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        IReadOnlyList<Comment> comments = await ListAsync(post.Id, null, 10);

        comments.Select(comment => comment.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task WithPosition_ReturnsOnlyNewerComments()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        await AddCommentAsync(post, author, "content-1");
        Comment second = await AddCommentAsync(post, author, "content-2");
        Comment third = await AddCommentAsync(post, author, "content-3");

        IReadOnlyList<Comment> comments = await ListAsync(post.Id, (second.CreatedAt, second.Id), 10);

        comments.Select(comment => comment.Id).Should().Equal(third.Id);
    }

    [Fact]
    public async Task ReturnsAtMostCount()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        Comment first = await AddCommentAsync(post, author, "content-1");
        Comment second = await AddCommentAsync(post, author, "content-2");
        await AddCommentAsync(post, author, "content-3");

        IReadOnlyList<Comment> comments = await ListAsync(post.Id, null, 2);

        comments.Select(comment => comment.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task CommentsWithSameCreatedAt_AreOrderedByIdAndPagedWithoutGaps()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        Comment one = Comment.Create(post.Id, author.Id, "content-1");
        Comment two = Comment.Create(post.Id, author.Id, "content-2");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(one, two);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Guid[] expectedOrder = new[] { one.Id, two.Id }
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<Comment> firstPage = await ListAsync(post.Id, null, 1);

        IReadOnlyList<Comment> secondPage = await ListAsync(post.Id, (firstPage[0].CreatedAt, firstPage[0].Id), 1);

        firstPage.Select(comment => comment.Id).Should().Equal(expectedOrder[0]);
        secondPage.Select(comment => comment.Id).Should().Equal(expectedOrder[1]);
    }

    [Fact]
    public async Task Counts_CountEachPostSeparatelyAndOmitPostsWithoutComments()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post twice = await AddPostAsync(author);
        Post once = await AddPostAsync(author);
        Post never = await AddPostAsync(author);
        await AddCommentAsync(twice, author, "content-1");
        await AddCommentAsync(twice, author, "content-2");
        await AddCommentAsync(once, author, "content-3");

        IReadOnlyDictionary<Guid, int> counts = await CountAsync([twice.Id, once.Id, never.Id]);

        counts[twice.Id].Should().Be(2);
        counts[once.Id].Should().Be(1);
        counts.Should().NotContainKey(never.Id);
    }

    [Fact]
    public async Task Counts_SkipDeletedCommentsDeletedAuthorsAndOtherPosts()
    {
        User author = await AddUserAsync("identity-id-1", "username-1");
        User deletedAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post asked = await AddPostAsync(author);
        Post notAsked = await AddPostAsync(author);
        await AddCommentAsync(asked, author, "content-1");
        Comment deleted = await AddCommentAsync(asked, author, "content-2");
        await AddCommentAsync(asked, deletedAuthor, "content-3");
        await AddCommentAsync(notAsked, author, "content-4");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Comment>().Single(comment => comment.Id == deleted.Id));
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == deletedAuthor.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        IReadOnlyDictionary<Guid, int> counts = await CountAsync([asked.Id]);

        counts.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, int>(asked.Id, 1));
    }

    [Fact]
    public async Task Counts_WithoutPostIds_ReturnNothing()
    {
        User author = await AddUserAsync("identity-id", "username");
        await AddCommentAsync(await AddPostAsync(author), author, "content-1");

        IReadOnlyDictionary<Guid, int> counts = await CountAsync([]);

        counts.Should().BeEmpty();
    }

    private async Task<IReadOnlyDictionary<Guid, int>> CountAsync(Guid[] postIds)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICommentRepository repository = scope.ServiceProvider.GetRequiredService<ICommentRepository>();

        return await repository.CountByPostsForReadAsync(postIds, TestContext.Current.CancellationToken);
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

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private async Task<Comment> AddCommentAsync(Post post, User author, string content)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Comment comment = Comment.Create(post.Id, author.Id, content);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(comment);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return comment;
    }

    private async Task<IReadOnlyList<Comment>> ListAsync(Guid postId, (DateTimeOffset CreatedAt, Guid Id)? after, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICommentRepository repository = scope.ServiceProvider.GetRequiredService<ICommentRepository>();

        return await repository.ListByPostForReadAsync(postId, new Guid("00000000-0000-0000-0000-000000000099"), after, count, TestContext.Current.CancellationToken);
    }
}
