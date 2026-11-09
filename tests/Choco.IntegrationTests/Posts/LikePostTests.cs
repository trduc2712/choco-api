using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Posts;

public sealed class LikePostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public LikePostTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/posts/{UnknownPostId}/like");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentLikeRequests_CreateOneLike()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage firstRequest = Request("PUT", post.Id);
        using HttpRequestMessage secondRequest = Request("PUT", post.Id);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<HttpResponseMessage> first = _client.SendAsync(firstRequest, cancellationToken);
        Task<HttpResponseMessage> second = _client.SendAsync(secondRequest, cancellationToken);
        await Task.WhenAll(first, second);

        using HttpResponseMessage firstResponse = await first;
        using HttpResponseMessage secondResponse = await second;
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        PostLike like = (await GetLikesAsync(post.Id)).Should().ContainSingle().Subject;
        like.UserId.Should().Be(caller.Id);
        like.LikedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatedLikeRequests_KeepTheFirstLike()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        using HttpRequestMessage firstRequest = Request("PUT", post.Id);
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        PostLike first = (await GetLikesAsync(post.Id)).Should().ContainSingle().Subject;
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage secondRequest = Request("PUT", post.Id);

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        PostLike kept = (await GetLikesAsync(post.Id)).Should().ContainSingle().Subject;
        kept.Id.Should().Be(first.Id);
        kept.LikedAt.Should().Be(first.LikedAt);
    }

    [Fact]
    public async Task CallerMayLikeTheirOwnPost()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        using HttpRequestMessage request = Request("PUT", post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetLikesAsync(post.Id)).Should().ContainSingle().Which.UserId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task RepeatedUnlikeRequests_RemoveOnlyTheCallersLike()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(other);
        Post otherPost = await AddPostAsync(other);
        await AddLikesAsync((post, caller), (post, other), (otherPost, caller));
        using HttpRequestMessage firstRequest = Request("DELETE", post.Id);
        using HttpRequestMessage secondRequest = Request("DELETE", post.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetLikesAsync(post.Id)).Should().ContainSingle().Which.UserId.Should().Be(other.Id);
        (await GetLikesAsync(otherPost.Id)).Should().ContainSingle().Which.UserId.Should().Be(caller.Id);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownPost_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(method, UnknownPostId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedPost_ReturnsNotFoundAndLikesNothing(string method)
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
        (await GetLikesAsync(post.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task PostOfDeletedAuthor_ReturnsNotFoundAndLikesNothing(string method)
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == author.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
        (await GetLikesAsync(post.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EmptyPostId_ReturnsValidationProblem(string method)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(method, Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("PostId");
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedCaller_ReturnsUnauthorized(string method)
    {
        User caller = await AddUserAsync("identity-id", "username");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, UnknownPostId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
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

    private Task<int> AddLikesAsync(params (Post Post, User User)[] likes)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(likes.Select(like => PostLike.Create(like.Post.Id, like.User.Id, now)));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<List<PostLike>> GetLikesAsync(Guid postId)
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<PostLike>().AsNoTracking()
            .Where(like => like.PostId == postId)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static HttpRequestMessage Request(string method, Guid postId)
    {
        HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/posts/{postId}/like");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }
}
