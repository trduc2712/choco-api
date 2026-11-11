using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Comments;

public sealed class CreateCommentTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CreateCommentTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/posts/{UnknownPostId}/comments")
        {
            Content = JsonContent.Create(new { content = "content" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NewCaller_CreatesUserAndCommentAndReturnsCreated()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username",
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request(post.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        User caller = await _factory.ExecuteDbContextAsync(db => db.Set<User>().AsNoTracking()
            .SingleAsync(user => user.IdentityId == "identity-id", TestContext.Current.CancellationToken));
        Comment comment = (await GetCommentsAsync()).Should().ContainSingle().Subject;
        response.Headers.Location.Should().Be(new Uri(_client.BaseAddress!, $"/api/v1/posts/{post.Id}/comments"));
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "postId", "authorId", "author", "content", "createdAt");
        body.GetProperty("id").GetGuid().Should().Be(comment.Id);
        body.GetProperty("postId").GetGuid().Should().Be(post.Id);
        body.GetProperty("authorId").GetGuid().Should().Be(caller.Id);
        body.GetProperty("author").GetProperty("username").GetString().Should().Be("username");
        body.GetProperty("author").GetProperty("displayName").GetString().Should().Be("first-name last-name");
        body.GetProperty("author").GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar.png");
        body.GetProperty("content").GetString().Should().Be("content");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().Be(ChocoApiFactory.StartTime);
        comment.PostId.Should().Be(post.Id);
        comment.AuthorId.Should().Be(caller.Id);
        comment.Content.Should().Be("content");
        comment.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        comment.IsDeleted.Should().BeFalse();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ExistingCaller_TrimsTheContentAndMayCommentOnTheirOwnPost()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        using HttpRequestMessage request = Request(post.Id, new { content = "  content  " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("content").GetString().Should().Be("content");
        (await GetCommentsAsync()).Should().ContainSingle().Which.AuthorId.Should().Be(caller.Id);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SameBodyTwice_AddsTwoComments()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage first = Request(post.Id, new { content = "content" });
        using HttpRequestMessage second = Request(post.Id, new { content = "content" });

        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetCommentsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task BodyNamingAnotherPost_IsIgnoredInFavorOfTheRoute()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post routePost = await AddPostAsync(author);
        Post bodyPost = await AddPostAsync(author);
        using HttpRequestMessage request = Request(routePost.Id, new { postId = bodyPost.Id, content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetCommentsAsync()).Should().ContainSingle().Which.PostId.Should().Be(routePost.Id);
    }

    [Fact]
    public async Task ContentAtMaxLength_ReturnsCreated()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        string content = new('c', Comment.ContentMaxLength);
        using HttpRequestMessage request = Request(post.Id, new { content });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetCommentsAsync()).Should().ContainSingle().Which.Content.Should().Be(content);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task BlankContent_ReturnsValidationProblemAndAddsNothing(string content)
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = Request(post.Id, new { content });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "content");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NullContent_IsRejectedBeforeTheHandlerAndAddsNothing()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = Request(post.Id, new { content = (string?)null });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("content");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ContentOverMaxLength_ReturnsValidationProblemAndAddsNothing()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage request = Request(post.Id, new { content = new string('c', Comment.ContentMaxLength + 1) });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "content");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyPostId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(Guid.Empty, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "postId");
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFoundAndAddsNothing()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(UnknownPostId, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFoundAndAddsNothing()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(post.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PostOfDeletedAuthor_ReturnsNotFoundAndAddsNothing()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == author.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(post.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorizedAndAddsNothing()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(post.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
        (await GetCommentsAsync()).Should().BeEmpty();
    }

    private static HttpRequestMessage Request(Guid postId, object body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/posts/{postId}/comments")
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

    private Task<List<Comment>> GetCommentsAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<Comment>().IgnoreQueryFilters().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
