using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Comments;

public sealed class GetPostCommentsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetPostCommentsTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/posts/{UnknownPostId}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostWithoutComments_ReturnsEmptyPage()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("items", "nextCursor");
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ReturnsActiveCommentsOfThePostOldestFirstWithTheirAuthors()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User deletedAuthor = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Post otherPost = await AddPostAsync(author);
        Comment first = await AddCommentAsync(post, caller, "content-1");
        Comment deleted = await AddCommentAsync(post, caller, "content-2");
        Comment second = await AddCommentAsync(post, author, "content-3");
        await AddCommentAsync(post, deletedAuthor, "content-4");
        await AddCommentAsync(otherPost, caller, "content-5");
        await DeleteCommentAsync(deleted);
        await DeleteUserAsync(deletedAuthor);
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("id").GetGuid()).Should().Equal(first.Id, second.Id);
        items.Select(item => item.GetProperty("content").GetString()).Should().Equal("content-1", "content-3");
        items.Select(item => item.GetProperty("authorId").GetGuid()).Should().Equal(caller.Id, author.Id);
        items.Select(item => item.GetProperty("author").GetProperty("username").GetString()).Should().Equal("username", "username-2");
        items[0].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "postId", "authorId", "author", "content", "createdAt");
        items[0].GetProperty("postId").GetGuid().Should().Be(post.Id);
        items[0].GetProperty("createdAt").GetDateTimeOffset().Should().Be(first.CreatedAt);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PagesThroughCommentsWithCursor()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment first = await AddCommentAsync(post, caller, "content-1");
        Comment second = await AddCommentAsync(post, caller, "content-2");
        Comment third = await AddCommentAsync(post, caller, "content-3");
        using HttpRequestMessage firstRequest = Request($"/api/v1/posts/{post.Id}/comments?limit=2");
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        JsonElement firstPage = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string cursor = firstPage.GetProperty("nextCursor").GetString()!;
        using HttpRequestMessage lastRequest = Request($"/api/v1/posts/{post.Id}/comments?limit=2&cursor={cursor}");

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

    [Fact]
    public async Task WithoutLimit_ReturnsTwentyCommentsAndNextCursor()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        await _factory.ExecuteDbContextAsync(db =>
        {
            for (int number = 1; number <= 21; number++)
            {
                _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
                db.Add(Comment.Create(post.Id, caller.Id, $"content-{number}"));
            }

            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("items").GetArrayLength().Should().Be(20);
        body.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("?limit=0", "Limit")]
    [InlineData("?limit=51", "Limit")]
    [InlineData("?cursor=not-a-cursor", "Cursor")]
    public async Task InvalidLimitOrCursor_ReturnsValidationProblem(string query, string field)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/posts/{UnknownPostId}/comments{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task EmptyPostId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/posts/{Guid.Empty}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "PostId");
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/posts/{UnknownPostId}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, caller, "content-1");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Fact]
    public async Task PostOfDeletedAuthor_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await AddCommentAsync(post, caller, "content-1");
        await DeleteUserAsync(author);
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await DeleteUserAsync(caller);
        using HttpRequestMessage request = Request($"/api/v1/posts/{UnknownPostId}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
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

    private async Task<Comment> AddCommentAsync(Post post, User author, string content)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Comment comment = Comment.Create(post.Id, author.Id, content);
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
