using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Me;

public sealed class GetRecommendedFeedTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string FeedPath = "/api/v1/me/feed/recommended";
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetRecommendedFeedTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, FeedPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NewCaller_CreatesLocalAccountAndGetsEmptyPage()
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
        using HttpRequestMessage request = CreateRequest(FeedPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task NobodyElseHasPosted_GetsEmptyPage()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await AddPostAsync(caller, "content-1");
        using HttpRequestMessage request = CreateRequest(FeedPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsOtherUsersPostsNewestFirstIncludingFollowedAccounts()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User followedAuthor = await AddUserAsync("identity-id-2", "username-2");
        User otherAuthor = await AddUserAsync("identity-id-3", "username-3");
        User deletedAuthor = await AddUserAsync("identity-id-4", "username-4");
        await FollowAsync(caller, followedAuthor);
        Post older = await AddPostAsync(followedAuthor, "content-1");
        Post deleted = await AddPostAsync(otherAuthor, "content-2");
        Post newer = await AddPostAsync(otherAuthor, "content-3");
        await AddPostAsync(caller, "content-4");
        await AddPostAsync(deletedAuthor, "content-5");
        await DeletePostAsync(deleted.Id);
        await DeleteUserAsync(deletedAuthor.Id);
        using HttpRequestMessage request = CreateRequest(FeedPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().Equal(
            new PostBody(newer.Id, otherAuthor.Id, new AuthorBody("username-3", "Display Name", null), "content-3", 0, false, newer.CreatedAt, newer.CreatedAt),
            new PostBody(older.Id, followedAuthor.Id, new AuthorBody("username-2", "Display Name", null), "content-1", 0, false, older.CreatedAt, older.CreatedAt));
        body.NextCursor.Should().BeNull();
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PagesThroughPostsWithCursor()
    {
        await AddUserAsync("identity-id", "username");
        User firstAuthor = await AddUserAsync("identity-id-2", "username-2");
        User secondAuthor = await AddUserAsync("identity-id-3", "username-3");
        Post first = await AddPostAsync(firstAuthor, "content-1");
        Post second = await AddPostAsync(secondAuthor, "content-2");
        Post third = await AddPostAsync(firstAuthor, "content-3");
        using HttpRequestMessage firstRequest = CreateRequest($"{FeedPath}?limit=2");
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        PageBody firstPage = (await firstResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        using HttpRequestMessage secondRequest = CreateRequest($"{FeedPath}?limit=2&cursor={firstPage.NextCursor}");

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstPage.Items.Select(post => post.Id).Should().Equal(third.Id, second.Id);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody secondPage = (await secondResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        secondPage.Items.Select(post => post.Id).Should().Equal(first.Id);
        secondPage.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task WithoutLimit_ReturnsTwentyPostsAndNextCursor()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            for (int number = 1; number <= 21; number++)
            {
                _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
                dbContext.Add(Post.Create($"content-{number}", author.Id));
            }

            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(FeedPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().HaveCount(20);
        body.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task LimitOutOfRange_ReturnsValidationProblem(int limit)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest($"{FeedPath}?limit={limit}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Limit");
    }

    [Fact]
    public async Task InvalidCursor_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest($"{FeedPath}?cursor=not-a-cursor");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Cursor");
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await DeleteUserAsync(caller.Id);
        using HttpRequestMessage request = CreateRequest(FeedPath);

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

    private Task FollowAsync(User follower, User followed)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(UserFollow.Create(follower.Id, followed.Id, _factory.TimeProvider.GetUtcNow()).Value);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
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

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private sealed record PageBody(List<PostBody> Items, string? NextCursor);

    private sealed record AuthorBody(string Username, string DisplayName, string? AvatarUrl);

    private sealed record PostBody(Guid Id, Guid AuthorId, AuthorBody Author, string Content, int LikeCount, bool IsLikedByMe, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
