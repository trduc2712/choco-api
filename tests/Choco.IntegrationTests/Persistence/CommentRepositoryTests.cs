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

        return await repository.ListByPostForReadAsync(postId, after, count, TestContext.Current.CancellationToken);
    }
}
