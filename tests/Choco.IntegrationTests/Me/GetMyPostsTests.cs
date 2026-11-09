using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Me;

public sealed class GetMyPostsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyPostsTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me/posts");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NewUser_GetsEmptyPage()
    {
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
        using HttpRequestMessage request = CreateRequest("/api/v1/me/posts");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ReturnsOwnPostsNewestFirst()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User otherUser = await AddUserAsync("identity-id-2", "username-2");
        Post older = await AddPostAsync(caller, "content-1");
        Post deleted = await AddPostAsync(caller, "content-2");
        Post newer = await AddPostAsync(caller, "content-3");
        await AddPostAsync(otherUser, "content-4");
        await DeletePostAsync(deleted.Id);
        using HttpRequestMessage request = CreateRequest("/api/v1/me/posts");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().Equal(
            new PostBody(newer.Id, caller.Id, new AuthorBody("username", "Display Name", null), "content-3", 0, false, newer.CreatedAt, newer.CreatedAt),
            new PostBody(older.Id, caller.Id, new AuthorBody("username", "Display Name", null), "content-1", 0, false, older.CreatedAt, older.CreatedAt));
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task PagesThroughPostsWithCursor()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post first = await AddPostAsync(caller, "content-1");
        Post second = await AddPostAsync(caller, "content-2");
        Post third = await AddPostAsync(caller, "content-3");
        using HttpRequestMessage firstRequest = CreateRequest("/api/v1/me/posts?limit=2");
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        PageBody firstPage = (await firstResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        using HttpRequestMessage secondRequest = CreateRequest($"/api/v1/me/posts?limit=2&cursor={firstPage.NextCursor}");

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.Items.Select(post => post.Id).Should().Equal(third.Id, second.Id);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody secondPage = (await secondResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        secondPage.Items.Select(post => post.Id).Should().Equal(first.Id);
        secondPage.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task LimitOutOfRange_ReturnsValidationProblem(int limit)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest($"/api/v1/me/posts?limit={limit}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Limit");
    }

    [Fact]
    public async Task InvalidCursor_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest("/api/v1/me/posts?cursor=not-a-cursor");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Cursor");
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == caller.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest("/api/v1/me/posts");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.Deleted");
    }

    private static HttpRequestMessage CreateRequest(string uri)
    {
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBeValidationProblemAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo([field]);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
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

    private sealed record PageBody(List<PostBody> Items, string? NextCursor);

    private sealed record AuthorBody(string Username, string DisplayName, string? AvatarUrl);

    private sealed record PostBody(Guid Id, Guid AuthorId, AuthorBody Author, string Content, int LikeCount, bool IsLikedByMe, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
