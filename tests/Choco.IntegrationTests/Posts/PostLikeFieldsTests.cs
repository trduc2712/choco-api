using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Posts;

public sealed class PostLikeFieldsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public PostLikeFieldsTests(ChocoApiFactory factory)
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
    [InlineData("get")]
    [InlineData("user-posts")]
    [InlineData("following-feed")]
    [InlineData("recommended-feed")]
    public async Task PostLikedByCallerAndOthers_ShowsCountOfActiveUsersAndLikedByMe(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-3", "username-3");
        User deleted = await AddUserAsync("identity-id-4", "username-4");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);
        await AddLikesAsync((post, caller), (post, other), (post, deleted));
        await DeleteUserAsync(deleted);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("likeCount").GetInt32().Should().Be(2);
        body.GetProperty("isLikedByMe").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("user-posts")]
    [InlineData("following-feed")]
    [InlineData("recommended-feed")]
    public async Task PostLikedOnlyByOthers_ShowsNotLikedByMe(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);
        await AddLikesAsync((post, other));

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("likeCount").GetInt32().Should().Be(1);
        body.GetProperty("isLikedByMe").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("user-posts")]
    [InlineData("following-feed")]
    [InlineData("recommended-feed")]
    public async Task PostWithoutLikes_ShowsZeroAndNotLikedByMe(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("likeCount").GetInt32().Should().Be(0);
        body.GetProperty("isLikedByMe").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EachPostOfAPage_ShowsItsOwnLikes()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-3", "username-3");
        Post first = await AddPostAsync(author);
        Post second = await AddPostAsync(author);
        Post third = await AddPostAsync(author);
        await AddLikesAsync((first, caller), (first, other), (third, other));

        JsonElement page = await GetAsync($"/api/v1/users/{author.Id}/posts");

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("likeCount").GetInt32(), item.GetProperty("isLikedByMe").GetBoolean()))
            .Should().Equal((third.Id, 1, false), (second.Id, 0, false), (first.Id, 2, true));
    }

    [Fact]
    public async Task OwnPostLikedByOthers_ShowsCountAndNotLikedByMeOnMyPosts()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        await AddLikesAsync((post, other));

        JsonElement page = await GetAsync("/api/v1/me/posts");

        JsonElement item = page.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("likeCount").GetInt32().Should().Be(1);
        item.GetProperty("isLikedByMe").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EditedPost_ShowsItsLikes()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        await AddLikesAsync((post, caller), (post, other));
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/posts/{post.Id}")
        {
            Content = JsonContent.Create(new { content = "edited-content" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("likeCount").GetInt32().Should().Be(2);
        body.GetProperty("isLikedByMe").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task LikeThenUnlike_IsReflectedByTheNextRequest()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);

        JsonElement afterLike = await SendThenGetAsync(HttpMethod.Put, post);
        JsonElement afterUnlike = await SendThenGetAsync(HttpMethod.Delete, post);

        afterLike.GetProperty("likeCount").GetInt32().Should().Be(1);
        afterLike.GetProperty("isLikedByMe").GetBoolean().Should().BeTrue();
        afterUnlike.GetProperty("likeCount").GetInt32().Should().Be(0);
        afterUnlike.GetProperty("isLikedByMe").GetBoolean().Should().BeFalse();
    }

    private async Task<JsonElement> GetPostBodyAsync(string endpoint, Post post, User author)
    {
        string path = endpoint switch
        {
            "get" => $"/api/v1/posts/{post.Id}",
            "user-posts" => $"/api/v1/users/{author.Id}/posts",
            "following-feed" => "/api/v1/me/feed/following",
            _ => "/api/v1/me/feed/recommended",
        };
        JsonElement body = await GetAsync(path);

        return endpoint == "get"
            ? body
            : body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
    }

    private async Task<JsonElement> SendThenGetAsync(HttpMethod method, Post post)
    {
        using HttpRequestMessage action = Request(method, $"/api/v1/posts/{post.Id}/like");
        using HttpResponseMessage actionResponse = await _client.SendAsync(action, TestContext.Current.CancellationToken);
        actionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return await GetAsync($"/api/v1/posts/{post.Id}");
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
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
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(post);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return post;
    }

    private Task FollowAsync(User follower, User followed)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(UserFollow.Create(follower.Id, followed.Id, _factory.TimeProvider.GetUtcNow()).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
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

    private Task<int> DeleteUserAsync(User user)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(candidate => candidate.Id == user.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
