using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Posts;

public sealed class GetPostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetPostTests(ChocoApiFactory factory)
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
        Post post = await AddPostAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExistingPost_ReturnsPost()
    {
        Post post = await AddPostAsync();
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        body.Should().Be(new PostBody(
            post.Id, post.AuthorId, new AuthorBody("username", "Display Name", "https://example.com/avatar.png"),
            "content", 0, false, ChocoApiFactory.StartTime, ChocoApiFactory.StartTime));
    }

    [Fact]
    public async Task AuthorWithoutAvatar_ReturnsNullAvatarUrl()
    {
        Post post = await AddPostAsync(avatarUrl: null);
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        body.Author.Should().Be(new AuthorBody("username", "Display Name", null));
    }

    [Fact]
    public async Task ChangedAuthorProfile_IsReflectedByTheNextRequest()
    {
        Post post = await AddPostAsync();
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            User author = dbContext.Set<User>().Single(user => user.Id == post.AuthorId);
            author.ChangeProfileName("username-2", "Display Name 2");
            author.ChangeAvatar("https://example.com/avatar-2.png");
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        body.Author.Should().Be(new AuthorBody("username-2", "Display Name 2", "https://example.com/avatar-2.png"));
    }

    [Fact]
    public async Task PostOfDeletedAuthor_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(identityId: "identity-id-2", username: "username-2");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == post.AuthorId));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBePostNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(UnknownPostId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBePostNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFound()
    {
        Post post = await AddPostAsync();
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(p => p.Id == post.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBePostNotFoundProblemAsync(response);
    }

    private static HttpRequestMessage CreateRequest(Guid id)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/posts/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBePostNotFoundProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Post.NotFound");
    }

    private async Task AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<Post> AddPostAsync(
        string? avatarUrl = "https://example.com/avatar.png", string identityId = "identity-id", string username = "username")
    {
        User user = User.Create(identityId, username, "Display Name", avatarUrl);
        Post post = Post.Create("content", user.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(user, post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private sealed record AuthorBody(string Username, string DisplayName, string? AvatarUrl);

    private sealed record PostBody(Guid Id, Guid AuthorId, AuthorBody Author, string Content, int LikeCount, bool IsLikedByMe, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
