using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Users;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Comments;

public sealed class ReplyToCommentTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownCommentId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ReplyToCommentTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/comments/{UnknownCommentId}/replies")
        {
            Content = JsonContent.Create(new { content = "content" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReplyToComment_AddsReplyOnTheSamePostAndReturnsCreated()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, author.Id, "content-1"));
        using HttpRequestMessage request = Request(parent.Id, new { content = "  content-2  " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Comment reply = (await GetRepliesAsync(parent.Id)).Should().ContainSingle().Subject;
        response.Headers.Location.Should().Be(new Uri(_client.BaseAddress!, $"/api/v1/comments/{parent.Id}/replies"));
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "postId", "parentId", "authorId", "author", "content", "replyCount", "likeCount", "dislikeCount", "myReaction", "isDeleted", "createdAt");
        body.GetProperty("id").GetGuid().Should().Be(reply.Id);
        body.GetProperty("postId").GetGuid().Should().Be(post.Id);
        body.GetProperty("parentId").GetGuid().Should().Be(parent.Id);
        body.GetProperty("authorId").GetGuid().Should().Be(caller.Id);
        body.GetProperty("author").GetProperty("username").GetString().Should().Be("username");
        body.GetProperty("content").GetString().Should().Be("content-2");
        body.GetProperty("replyCount").GetInt32().Should().Be(0);
        body.GetProperty("likeCount").GetInt32().Should().Be(0);
        body.GetProperty("dislikeCount").GetInt32().Should().Be(0);
        body.GetProperty("myReaction").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
        body.GetProperty("createdAt").GetDateTimeOffset().Should().Be(_factory.TimeProvider.GetUtcNow());
        reply.PostId.Should().Be(post.Id);
        reply.AuthorId.Should().Be(caller.Id);
        reply.Content.Should().Be("content-2");
        reply.ParentId.Should().Be(parent.Id);
        reply.RootId.Should().Be(parent.Id);
        reply.Depth.Should().Be(1);
    }

    [Fact]
    public async Task ReplyToReply_KeepsTheTopLevelCommentAsRoot()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment root = Comment.Create(post.Id, caller.Id, "content-1");
        Comment parent = root.Reply(caller.Id, "content-2").Value;
        await AddCommentAsync(root, parent);
        using HttpRequestMessage request = Request(parent.Id, new { content = "content-3" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Comment reply = (await GetRepliesAsync(parent.Id)).Should().ContainSingle().Subject;
        reply.RootId.Should().Be(root.Id);
        reply.Depth.Should().Be(2);
    }

    [Fact]
    public async Task CommentAtMaxDepth_ReturnsBadRequestAndAddsNothing()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment deepest = await AddChainAsync(post, caller, Comment.MaxDepth);
        using HttpRequestMessage request = Request(deepest.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Comment.MaxDepthReached");
        (await GetRepliesAsync(deepest.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task BlankContent_ReturnsValidationProblem()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, caller.Id, "content"));
        using HttpRequestMessage request = Request(parent.Id, new { content = " " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "content");
        (await GetRepliesAsync(parent.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyCommentId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(Guid.Empty, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "commentId");
    }

    [Fact]
    public async Task UnknownComment_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(UnknownCommentId, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task DeletedComment_ReturnsNotFoundAndAddsNothing()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, caller.Id, "content"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Comment>().Single(candidate => candidate.Id == parent.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(parent.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
        (await GetRepliesAsync(parent.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task CommentOfDeletedAuthor_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, author.Id, "content"));
        await DeleteUserAsync(author);
        using HttpRequestMessage request = Request(parent.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task CommentOnDeletedPost_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, caller.Id, "content"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(parent.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockWithTheCommentAuthor_ReturnsForbiddenAndAddsNothing(bool callerBlocks)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, author.Id, "content"));
        await (callerBlocks
            ? UserBlockTestData.AddBlockAsync(_factory, caller, author)
            : UserBlockTestData.AddBlockAsync(_factory, author, caller));
        using HttpRequestMessage request = Request(parent.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await GetRepliesAsync(parent.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task BlockWithThePostAuthor_ReturnsForbidden()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User postAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(postAuthor);
        Comment parent = await AddCommentAsync(Comment.Create(post.Id, caller.Id, "content"));
        await UserBlockTestData.AddBlockAsync(_factory, postAuthor, caller);
        using HttpRequestMessage request = Request(parent.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await DeleteUserAsync(caller);
        using HttpRequestMessage request = Request(UnknownCommentId, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
    }

    private static HttpRequestMessage Request(Guid commentId, object body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/comments/{commentId}/replies")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static async Task ShouldBeValidationProblemAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(post);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return post;
    }

    private async Task<Comment> AddCommentAsync(params Comment[] comments)
    {
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(comments);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comments[^1];
    }

    private Task<Comment> AddChainAsync(Post post, User author, int depth)
    {
        Comment[] chain = new Comment[depth + 1];
        chain[0] = Comment.Create(post.Id, author.Id, "content");
        for (int level = 1; level <= depth; level++)
        {
            chain[level] = chain[level - 1].Reply(author.Id, "content").Value;
        }

        return AddCommentAsync(chain);
    }

    private Task<List<Comment>> GetRepliesAsync(Guid parentId)
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<Comment>().IgnoreQueryFilters().AsNoTracking()
            .Where(comment => comment.ParentId == parentId)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private Task<int> DeleteUserAsync(User user)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(candidate => candidate.Id == user.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
