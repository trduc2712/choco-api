using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Posts;

public sealed class PostCommentCountFieldsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public PostCommentCountFieldsTests(ChocoApiFactory factory)
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
    public async Task PostWithComments_ShowsTheCountOfActiveComments(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User deleted = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);
        await AddCommentAsync(post, caller);
        await AddCommentAsync(post, author);
        Comment removed = await AddCommentAsync(post, caller);
        await AddCommentAsync(post, deleted);
        await DeleteCommentAsync(removed);
        await DeleteUserAsync(deleted);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("commentCount").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("user-posts")]
    [InlineData("following-feed")]
    [InlineData("recommended-feed")]
    public async Task PostWithoutComments_ShowsZero(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("commentCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task EachPostOfAPage_ShowsItsOwnCount()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post first = await AddPostAsync(author);
        Post second = await AddPostAsync(author);
        Post third = await AddPostAsync(author);
        await AddCommentAsync(first, caller);
        await AddCommentAsync(first, author);
        await AddCommentAsync(third, caller);

        JsonElement page = await GetAsync($"/api/v1/users/{author.Id}/posts");

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("commentCount").GetInt32()))
            .Should().Equal((third.Id, 1), (second.Id, 0), (first.Id, 2));
    }

    [Fact]
    public async Task OwnPostOnMyPosts_ShowsItsCount()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, other);

        JsonElement page = await GetAsync("/api/v1/me/posts");

        page.GetProperty("items").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("commentCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task EditedPost_ShowsItsCount()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, other);
        await AddCommentAsync(post, caller);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{post.Id}");
        request.Content = JsonContent.Create(new { content = "edited-content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("commentCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task AddingThenDeletingAComment_IsReflectedByTheNextRequest()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);

        JsonElement created = await SendAsync(HttpMethod.Post, $"/api/v1/posts/{post.Id}/comments", new { content = "content" });
        JsonElement afterAdd = await GetAsync($"/api/v1/posts/{post.Id}");
        await SendAsync(HttpMethod.Delete, $"/api/v1/comments/{created.GetProperty("id").GetGuid()}", null);
        JsonElement afterDelete = await GetAsync($"/api/v1/posts/{post.Id}");

        afterAdd.GetProperty("commentCount").GetInt32().Should().Be(1);
        afterDelete.GetProperty("commentCount").GetInt32().Should().Be(0);
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

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body)
    {
        using HttpRequestMessage request = Request(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        return response.StatusCode == HttpStatusCode.NoContent
            ? default
            : await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
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

    private async Task<Comment> AddCommentAsync(Post post, User author)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Comment comment = Comment.Create(post.Id, author.Id, "content");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(comment);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comment;
    }

    private Task FollowAsync(User follower, User followed)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(UserFollow.Create(follower.Id, followed.Id, _factory.TimeProvider.GetUtcNow()).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
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
