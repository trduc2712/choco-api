using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Posts;

public sealed class PostTopCommentFieldsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public PostTopCommentFieldsTests(ChocoApiFactory factory)
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
    public async Task PostWithComments_ShowsTheTopLevelCommentWithTheMostRepliesInItsConversation(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User commenter = await AddUserAsync("identity-id-3", "username-3", "Display Name 3", "https://example.com/avatar-3.png");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);
        Comment top = await AddCommentAsync(post, commenter, "content-1");
        Comment reply = await AddReplyAsync(top, caller);
        await AddReplyAsync(reply, author);
        Comment newer = await AddCommentAsync(post, author, "content-2");
        await AddReplyAsync(newer, caller);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        JsonElement topComment = body.GetProperty("topComment");
        topComment.GetProperty("id").GetGuid().Should().Be(top.Id);
        topComment.GetProperty("content").GetString().Should().Be("content-1");
        JsonElement commentAuthor = topComment.GetProperty("author");
        commentAuthor.GetProperty("username").GetString().Should().Be("username-3");
        commentAuthor.GetProperty("displayName").GetString().Should().Be("Display Name 3");
        commentAuthor.GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar-3.png");
    }

    [Theory]
    [InlineData("get")]
    [InlineData("user-posts")]
    [InlineData("following-feed")]
    [InlineData("recommended-feed")]
    public async Task PostWithoutComments_ShowsNoTopComment(string endpoint)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await FollowAsync(caller, author);

        JsonElement body = await GetPostBodyAsync(endpoint, post, author);

        body.GetProperty("topComment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CommentsWithTheSameNumberOfReplies_ShowTheNewest()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment older = await AddCommentAsync(post, author);
        await AddReplyAsync(older, caller);
        Comment newer = await AddCommentAsync(post, author);
        await AddReplyAsync(newer, caller);

        JsonElement body = await GetAsync($"/api/v1/posts/{post.Id}");

        body.GetProperty("topComment").GetProperty("id").GetGuid().Should().Be(newer.Id);
    }

    [Fact]
    public async Task DeletedReplies_DoNotCount()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment older = await AddCommentAsync(post, author);
        Comment firstRemoved = await AddReplyAsync(older, caller);
        Comment secondRemoved = await AddReplyAsync(older, caller);
        Comment newer = await AddCommentAsync(post, author);
        await AddReplyAsync(newer, caller);
        await DeleteCommentAsync(firstRemoved);
        await DeleteCommentAsync(secondRemoved);

        JsonElement body = await GetAsync($"/api/v1/posts/{post.Id}");

        body.GetProperty("topComment").GetProperty("id").GetGuid().Should().Be(newer.Id);
    }

    [Theory]
    [InlineData("deleted-comment")]
    [InlineData("suspended-author")]
    [InlineData("deleted-author")]
    [InlineData("blocked-by-caller")]
    [InlineData("blocking-caller")]
    public async Task HiddenTopComment_GivesWayToTheNextVisibleComment(string reason)
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User commenter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Comment hidden = await AddCommentAsync(post, commenter);
        await AddReplyAsync(hidden, author);
        await AddReplyAsync(hidden, author);
        Comment visible = await AddCommentAsync(post, author);
        await AddReplyAsync(visible, author);
        await HideAsync(reason, hidden, commenter, caller);

        JsonElement body = await GetAsync($"/api/v1/posts/{post.Id}");

        body.GetProperty("topComment").GetProperty("id").GetGuid().Should().Be(visible.Id);
    }

    [Fact]
    public async Task RepliesUnderADeletedTopLevelComment_AreNeverShown()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment root = await AddCommentAsync(post, author);
        await AddReplyAsync(root, caller);
        await DeleteCommentAsync(root);

        JsonElement body = await GetAsync($"/api/v1/posts/{post.Id}");

        body.GetProperty("topComment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task EachPostOfAPage_ShowsItsOwnTopComment()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post first = await AddPostAsync(author);
        Post second = await AddPostAsync(author);
        Post third = await AddPostAsync(author);
        Comment firstComment = await AddCommentAsync(first, caller);
        Comment thirdComment = await AddCommentAsync(third, author);

        JsonElement page = await GetAsync($"/api/v1/users/{author.Id}/posts");

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), TopCommentId(item)))
            .Should().Equal((third.Id, thirdComment.Id), (second.Id, (Guid?)null), (first.Id, firstComment.Id));
    }

    [Fact]
    public async Task MorePostsWithComments_DoNotAddQueries()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post first = await AddPostAsync(author);
        await AddReplyAsync(await AddCommentAsync(first, caller), author);
        int onePostQueries = await CountQueriesAsync($"/api/v1/users/{author.Id}/posts");
        for (int index = 0; index < 4; index++)
        {
            Post post = await AddPostAsync(author);
            await AddReplyAsync(await AddCommentAsync(post, caller), author);
        }

        int fivePostsQueries = await CountQueriesAsync($"/api/v1/users/{author.Id}/posts");

        fivePostsQueries.Should().Be(onePostQueries);
    }

    [Fact]
    public async Task NewPost_ShowsNoTopComment()
    {
        await AddUserAsync("identity-id", "username");

        JsonElement body = await SendAsync(HttpMethod.Post, "/api/v1/posts", new { content = "content" });

        body.GetProperty("topComment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task EditedPost_ShowsItsTopComment()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User other = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment comment = await AddCommentAsync(post, other);

        JsonElement body = await SendAsync(HttpMethod.Put, $"/api/v1/posts/{post.Id}", new { content = "edited-content" });

        body.GetProperty("topComment").GetProperty("id").GetGuid().Should().Be(comment.Id);
    }

    private static Guid? TopCommentId(JsonElement item)
    {
        JsonElement topComment = item.GetProperty("topComment");
        return topComment.ValueKind == JsonValueKind.Null ? null : topComment.GetProperty("id").GetGuid();
    }

    private async Task HideAsync(string reason, Comment comment, User commenter, User caller)
    {
        switch (reason)
        {
            case "deleted-comment":
                await DeleteCommentAsync(comment);
                break;
            case "suspended-author":
                await SuspendAsync(commenter, caller);
                break;
            case "deleted-author":
                await DeleteUserAsync(commenter);
                break;
            case "blocked-by-caller":
                await BlockAsync(caller, commenter);
                break;
            default:
                await BlockAsync(commenter, caller);
                break;
        }
    }

    private async Task<int> CountQueriesAsync(string path)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        _factory.DbCommands.Reset();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return _factory.DbCommands.Count;
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

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object body)
    {
        using HttpRequestMessage request = Request(method, path);
        request.Content = JsonContent.Create(body);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
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

    private async Task<User> AddUserAsync(
        string identityId, string username, string displayName = "Display Name", string? avatarUrl = null)
    {
        User user = User.Create(identityId, username, displayName, avatarUrl);
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

    private Task<Comment> AddCommentAsync(Post post, User author, string content = "content")
    {
        return SaveCommentAsync(Comment.Create(post.Id, author.Id, content));
    }

    private Task<Comment> AddReplyAsync(Comment parent, User author)
    {
        return SaveCommentAsync(parent.Reply(author.Id, "content").Value);
    }

    private async Task<Comment> SaveCommentAsync(Comment comment)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
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

    private Task BlockAsync(User blocker, User blocked)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(UserBlock.Create(blocker.Id, blocked.Id, _factory.TimeProvider.GetUtcNow()).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<int> SuspendAsync(User user, User suspendedBy)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Set<User>().Single(candidate => candidate.Id == user.Id)
                .Suspend("reason", suspendedBy.Id, _factory.TimeProvider.GetUtcNow());
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
