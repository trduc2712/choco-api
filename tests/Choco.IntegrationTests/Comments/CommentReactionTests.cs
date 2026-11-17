using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Comments;

public sealed class CommentReactionTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownCommentId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CommentReactionTests(ChocoApiFactory factory)
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

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task NoToken_ReturnsUnauthorized(string method)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/comments/{UnknownCommentId}/reaction")
        {
            Content = method == "PUT" ? JsonContent.Create(new { type = "like" }) : null
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("like", CommentReactionType.Like)]
    [InlineData("dislike", CommentReactionType.Dislike)]
    public async Task SetReaction_StoresTheCallersReaction(string type, CommentReactionType expected)
    {
        User caller = await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = SetRequest(comment.Id, new { type });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        CommentReaction reaction = (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle().Subject;
        reaction.UserId.Should().Be(caller.Id);
        reaction.Type.Should().Be(expected);
        reaction.ReactedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SetReaction_WithTheOtherType_ChangesTheExistingReaction()
    {
        await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage firstRequest = SetRequest(comment.Id, new { type = "like" });
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        CommentReaction first = (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle().Subject;
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage secondRequest = SetRequest(comment.Id, new { type = "dislike" });

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        CommentReaction changed = (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle().Subject;
        changed.Id.Should().Be(first.Id);
        changed.Type.Should().Be(CommentReactionType.Dislike);
        changed.ReactedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task SetReaction_WithTheSameType_KeepsTheFirstReaction()
    {
        await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage firstRequest = SetRequest(comment.Id, new { type = "like" });
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        CommentReaction first = (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle().Subject;
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage secondRequest = SetRequest(comment.Id, new { type = "like" });

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        CommentReaction kept = (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle().Subject;
        kept.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task ConcurrentSetReactionRequests_CreateOneReaction()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage firstRequest = SetRequest(comment.Id, new { type = "like" });
        using HttpRequestMessage secondRequest = SetRequest(comment.Id, new { type = "like" });
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<HttpResponseMessage> first = _client.SendAsync(firstRequest, cancellationToken);
        Task<HttpResponseMessage> second = _client.SendAsync(secondRequest, cancellationToken);
        await Task.WhenAll(first, second);

        using HttpResponseMessage firstResponse = await first;
        using HttpResponseMessage secondResponse = await second;
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle()
            .Which.UserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task CallerMayReactToTheirOwnComment()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(caller);
        using HttpRequestMessage request = SetRequest(comment.Id, new { type = "like" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle()
            .Which.UserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task RepeatedRemoveReactionRequests_RemoveOnlyTheCallersReaction()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Comment comment = await AddCommentOnNewPostAsync(other);
        Comment otherComment = await AddCommentOnNewPostAsync(other);
        await CommentReactionTestData.AddReactionsAsync(
            _factory,
            (comment, caller, CommentReactionType.Like),
            (comment, other, CommentReactionType.Dislike),
            (otherComment, caller, CommentReactionType.Like));
        using HttpRequestMessage firstRequest = RemoveRequest(comment.Id);
        using HttpRequestMessage secondRequest = RemoveRequest(comment.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().ContainSingle()
            .Which.UserId.Should().Be(other.Id);
        (await CommentReactionTestData.GetReactionsAsync(_factory, otherComment.Id)).Should().ContainSingle()
            .Which.UserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task UnknownType_ReturnsValidationProblemAndStoresNothing()
    {
        await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = SetRequest(comment.Id, new { type = "type" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "type");
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EmptyCommentId_ReturnsValidationProblem(string method)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(method, Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "commentId");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownComment_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(method, UnknownCommentId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedComment_ReturnsNotFoundAndStoresNothing(string method)
    {
        await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Comment>().Single(candidate => candidate.Id == comment.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CommentOfDeletedAuthor_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Comment comment = await AddCommentOnNewPostAsync(author);
        await DeleteUserAsync(author);
        using HttpRequestMessage request = Request(method, comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CommentOnDeletedPost_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentOnNewPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(post => post.Id == comment.PostId));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockWithTheCommentAuthor_ReturnsForbiddenAndStoresNothing(bool callerBlocks)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment comment = await AddCommentAsync(Comment.Create(post.Id, author.Id, "content"));
        await (callerBlocks
            ? UserBlockTestData.AddBlockAsync(_factory, caller, author)
            : UserBlockTestData.AddBlockAsync(_factory, author, caller));
        using HttpRequestMessage request = SetRequest(comment.Id, new { type = "like" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task BlockWithThePostAuthor_ReturnsForbidden()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User postAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(postAuthor);
        Comment comment = await AddCommentAsync(Comment.Create(post.Id, caller.Id, "content"));
        await UserBlockTestData.AddBlockAsync(_factory, postAuthor, caller);
        using HttpRequestMessage request = SetRequest(comment.Id, new { type = "like" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
    }

    [Fact]
    public async Task RemoveReaction_AfterABlock_StillRemovesTheCallersReaction()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Comment comment = await AddCommentOnNewPostAsync(author);
        await CommentReactionTestData.AddReactionsAsync(_factory, (comment, caller, CommentReactionType.Dislike));
        await UserBlockTestData.AddBlockAsync(_factory, caller, author);
        using HttpRequestMessage request = RemoveRequest(comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedCaller_ReturnsUnauthorized(string method)
    {
        User caller = await AddUserAsync("identity-id", "username");
        await DeleteUserAsync(caller);
        using HttpRequestMessage request = Request(method, UnknownCommentId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
    }

    [Fact]
    public async Task DeletingTheComment_RemovesItsReactions()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Comment comment = await AddCommentOnNewPostAsync(caller);
        Comment otherComment = await AddCommentOnNewPostAsync(other);
        await CommentReactionTestData.AddReactionsAsync(
            _factory,
            (comment, caller, CommentReactionType.Like),
            (comment, other, CommentReactionType.Dislike),
            (otherComment, other, CommentReactionType.Like));
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/comments/{comment.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentReactionTestData.GetReactionsAsync(_factory, comment.Id)).Should().BeEmpty();
        (await CommentReactionTestData.GetReactionsAsync(_factory, otherComment.Id)).Should().ContainSingle();
    }

    private static HttpRequestMessage Request(string method, Guid commentId)
    {
        return method == "PUT" ? SetRequest(commentId, new { type = "like" }) : RemoveRequest(commentId);
    }

    private static HttpRequestMessage SetRequest(Guid commentId, object body)
    {
        HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/comments/{commentId}/reaction")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static HttpRequestMessage RemoveRequest(Guid commentId)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/comments/{commentId}/reaction");
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

    private async Task<Comment> AddCommentAsync(Comment comment)
    {
        comment.ClearDomainEvents();
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(comment);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comment;
    }

    private async Task<Comment> AddCommentOnNewPostAsync(User author)
    {
        Post post = await AddPostAsync(author);
        return await AddCommentAsync(Comment.Create(post.Id, author.Id, "content"));
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
