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

namespace Choco.IntegrationTests.Comments;

public sealed class GetCommentRepliesTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownCommentId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetCommentRepliesTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/comments/{UnknownCommentId}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReturnsDirectRepliesOldestFirstWithTheirReplyCounts()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment parent = await AddAsync(Comment.Create(post.Id, author.Id, "content-1"));
        Comment otherParent = await AddAsync(Comment.Create(post.Id, author.Id, "content-2"));
        Comment first = await AddAsync(parent.Reply(caller.Id, "content-3").Value);
        Comment second = await AddAsync(parent.Reply(author.Id, "content-4").Value);
        await AddAsync(first.Reply(author.Id, "content-5").Value);
        await AddAsync(first.Reply(author.Id, "content-6").Value);
        await AddAsync(otherParent.Reply(caller.Id, "content-7").Value);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("id").GetGuid()).Should().Equal(first.Id, second.Id);
        items.Select(item => item.GetProperty("parentId").GetGuid()).Should().Equal(parent.Id, parent.Id);
        items.Select(item => item.GetProperty("content").GetString()).Should().Equal("content-3", "content-4");
        items.Select(item => item.GetProperty("author").GetProperty("username").GetString()).Should().Equal("username", "username-2");
        items.Select(item => item.GetProperty("replyCount").GetInt32()).Should().Equal(2, 0);
        items.Select(item => item.GetProperty("isDeleted").GetBoolean()).Should().Equal(false, false);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeletedReplyWithReplies_IsAPlaceholderAndWithoutReplies_IsLeftOut()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment withReplies = await AddAsync(parent.Reply(caller.Id, "content-2").Value);
        Comment withoutReplies = await AddAsync(parent.Reply(caller.Id, "content-3").Value);
        await AddAsync(withReplies.Reply(caller.Id, "content-4").Value);
        await DeleteCommentAsync(withReplies);
        await DeleteCommentAsync(withoutReplies);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        ShouldBePlaceholder(item, withReplies.Id, parent.Id, 1);
    }

    [Fact]
    public async Task ReplyOfDeletedAccountWithReplies_IsAPlaceholder()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User deletedAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment withReplies = await AddAsync(parent.Reply(deletedAuthor.Id, "content-2").Value);
        await AddAsync(parent.Reply(deletedAuthor.Id, "content-3").Value);
        await AddAsync(withReplies.Reply(caller.Id, "content-4").Value);
        await DeleteUserAsync(deletedAuthor);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        ShouldBePlaceholder(item, withReplies.Id, parent.Id, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReplyOfBlockedUserWithReplies_IsAPlaceholderForTheCaller(bool callerBlocks)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User blocked = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment withReplies = await AddAsync(parent.Reply(blocked.Id, "content-2").Value);
        await AddAsync(parent.Reply(blocked.Id, "content-3").Value);
        await AddAsync(withReplies.Reply(caller.Id, "content-4").Value);
        await (callerBlocks
            ? UserBlockTestData.AddBlockAsync(_factory, caller, blocked)
            : UserBlockTestData.AddBlockAsync(_factory, blocked, caller));
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        ShouldBePlaceholder(item, withReplies.Id, parent.Id, 1);
    }

    [Fact]
    public async Task ReplyCount_CountsPlaceholdersButNotHiddenReplies()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment reply = await AddAsync(parent.Reply(caller.Id, "content-2").Value);
        await AddAsync(reply.Reply(caller.Id, "content-3").Value);
        Comment placeholder = await AddAsync(reply.Reply(caller.Id, "content-4").Value);
        Comment hidden = await AddAsync(reply.Reply(caller.Id, "content-5").Value);
        await AddAsync(placeholder.Reply(caller.Id, "content-6").Value);
        await DeleteCommentAsync(placeholder);
        await DeleteCommentAsync(hidden);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(response)).Should().ContainSingle()
            .Which.GetProperty("replyCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task RepliesOfADeletedComment_AreListed()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment reply = await AddAsync(parent.Reply(caller.Id, "content-2").Value);
        await DeleteCommentAsync(parent);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(response)).Should().ContainSingle()
            .Which.GetProperty("id").GetGuid().Should().Be(reply.Id);
    }

    [Fact]
    public async Task PagesThroughRepliesWithCursor()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content-1"));
        Comment first = await AddAsync(parent.Reply(caller.Id, "content-2").Value);
        Comment second = await AddAsync(parent.Reply(caller.Id, "content-3").Value);
        Comment third = await AddAsync(parent.Reply(caller.Id, "content-4").Value);
        using HttpRequestMessage firstRequest = Request($"/api/v1/comments/{parent.Id}/replies?limit=2");
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        JsonElement firstPage = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string cursor = firstPage.GetProperty("nextCursor").GetString()!;
        using HttpRequestMessage lastRequest = Request($"/api/v1/comments/{parent.Id}/replies?limit=2&cursor={cursor}");

        using HttpResponseMessage lastResponse = await _client.SendAsync(lastRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Should().Equal(first.Id, second.Id);
        lastResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement lastPage = await lastResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        lastPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Should().Equal(third.Id);
        lastPage.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("?limit=0", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task InvalidLimitOrCursor_ReturnsValidationProblem(string query, string field)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/comments/{UnknownCommentId}/replies{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task EmptyCommentId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/comments/{Guid.Empty}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "commentId");
    }

    [Fact]
    public async Task UnknownComment_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/comments/{UnknownCommentId}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task CommentOnDeletedPost_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task CommentOnPostOfBlockedAuthor_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User postAuthor = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(postAuthor);
        Comment parent = await AddAsync(Comment.Create(post.Id, caller.Id, "content"));
        await UserBlockTestData.AddBlockAsync(_factory, postAuthor, caller);
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    private static void ShouldBePlaceholder(JsonElement item, Guid id, Guid parentId, int replyCount)
    {
        item.GetProperty("id").GetGuid().Should().Be(id);
        item.GetProperty("parentId").GetGuid().Should().Be(parentId);
        item.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        item.GetProperty("authorId").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("author").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("replyCount").GetInt32().Should().Be(replyCount);
    }

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("items").EnumerateArray().ToArray();
    }

    private static HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
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

    private async Task<Comment> AddAsync(Comment comment)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(comment);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comment;
    }

    private Task<int> DeleteCommentAsync(Comment comment)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Comment>().Single(candidate => candidate.Id == comment.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
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
