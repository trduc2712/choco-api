using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

namespace Choco.IntegrationTests.Users;

public sealed class UserBlockVisibilityTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UserBlockVisibilityTests(ChocoApiFactory factory)
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecommendedFeed_LeavesOutPostsOfBlockedUsers(bool callerBlocked)
    {
        (User caller, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        await NotificationTestData.AddPostAsync(_factory, blocked, "blocked content");
        await NotificationTestData.AddPostAsync(_factory, visible, "visible content");

        JsonElement body = await GetAsync("/api/v1/me/feed/recommended");

        Items(body).Select(item => item.GetProperty("content").GetString()).Should().Equal("visible content");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FollowingFeed_LeavesOutPostsOfBlockedUsers(bool callerBlocked)
    {
        (User caller, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        await NotificationTestData.AddPostAsync(_factory, blocked, "blocked content");
        await NotificationTestData.AddPostAsync(_factory, visible, "visible content");
        await UserBlockTestData.AddFollowAsync(_factory, caller, blocked);
        await UserBlockTestData.AddFollowAsync(_factory, caller, visible);

        JsonElement body = await GetAsync("/api/v1/me/feed/following");

        Items(body).Select(item => item.GetProperty("content").GetString()).Should().Equal("visible content");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UserPosts_OfABlockedUser_AreEmpty(bool callerBlocked)
    {
        (_, User blocked, _) = await ArrangeAsync(callerBlocked);
        await NotificationTestData.AddPostAsync(_factory, blocked);

        JsonElement body = await GetAsync($"/api/v1/users/{blocked.Id}/posts");

        Items(body).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetPost_OfABlockedUser_ReturnsNotFound(bool callerBlocked)
    {
        (_, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        Post blockedPost = await NotificationTestData.AddPostAsync(_factory, blocked);
        Post visiblePost = await NotificationTestData.AddPostAsync(_factory, visible);

        HttpStatusCode blockedStatus = await StatusOfAsync($"/api/v1/posts/{blockedPost.Id}");
        HttpStatusCode visibleStatus = await StatusOfAsync($"/api/v1/posts/{visiblePost.Id}");

        blockedStatus.Should().Be(HttpStatusCode.NotFound);
        visibleStatus.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostComments_LeaveOutCommentsOfBlockedUsers(bool callerBlocked)
    {
        (_, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        await NotificationTestData.AddCommentAsync(_factory, post, blocked, "blocked comment");
        await NotificationTestData.AddCommentAsync(_factory, post, visible, "visible comment");

        JsonElement body = await GetAsync($"/api/v1/posts/{post.Id}/comments");

        Items(body).Select(item => item.GetProperty("content").GetString()).Should().Equal("visible comment");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostComments_OfABlockedAuthorsPost_ReturnNotFound(bool callerBlocked)
    {
        (_, User blocked, _) = await ArrangeAsync(callerBlocked);
        Post post = await NotificationTestData.AddPostAsync(_factory, blocked);

        HttpStatusCode status = await StatusOfAsync($"/api/v1/posts/{post.Id}/comments");

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAndSuggestions_LeaveOutBlockedUsers(bool callerBlocked)
    {
        (_, User blocked, User visible) = await ArrangeAsync(callerBlocked);

        JsonElement search = await GetAsync("/api/v1/users/search?q=username");
        JsonElement suggestions = await GetAsync("/api/v1/users/suggestions?q=username");

        Items(search).Select(item => item.GetProperty("username").GetString()).Should().Equal(visible.Username);
        suggestions.EnumerateArray().Select(item => item.GetProperty("username").GetString()).Should().Equal(visible.Username);
        blocked.Username.Should().NotBe(visible.Username);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FollowerAndFollowingLists_LeaveOutBlockedUsers(bool callerBlocked)
    {
        (User caller, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        User listed = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        await UserBlockTestData.AddFollowAsync(_factory, blocked, listed);
        await UserBlockTestData.AddFollowAsync(_factory, visible, listed);
        await UserBlockTestData.AddFollowAsync(_factory, listed, blocked);
        await UserBlockTestData.AddFollowAsync(_factory, listed, visible);

        JsonElement followers = await GetAsync($"/api/v1/users/{listed.Id}/followers");
        JsonElement following = await GetAsync($"/api/v1/users/{listed.Id}/following");

        Items(followers).Select(item => item.GetProperty("username").GetString()).Should().Equal(visible.Username);
        Items(following).Select(item => item.GetProperty("username").GetString()).Should().Equal(visible.Username);
        caller.Id.Should().NotBe(listed.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Notifications_LeaveOutBlockedActors(bool callerBlocked)
    {
        (User caller, User blocked, User visible) = await ArrangeAsync(callerBlocked);
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        await NotificationTestData.AddNotificationAsync(_factory, caller, blocked, now);
        await NotificationTestData.AddNotificationAsync(_factory, caller, visible, now.AddMinutes(1));

        JsonElement list = await GetAsync("/api/v1/me/notifications");
        JsonElement count = await GetAsync("/api/v1/me/notifications/unread-count");

        Items(list).Select(item => item.GetProperty("actor").GetProperty("username").GetString())
            .Should().Equal(visible.Username);
        count.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Profile_OfABlockedUser_StaysOpenAndReportsWhoBlocked(bool callerBlocked, bool expectedIsBlockedByMe)
    {
        (_, User blocked, _) = await ArrangeAsync(callerBlocked);

        JsonElement byId = await GetAsync($"/api/v1/users/{blocked.Id}");
        JsonElement byUsername = await GetAsync($"/api/v1/users/by-username/{blocked.Username}");

        byId.GetProperty("isBlockedByMe").GetBoolean().Should().Be(expectedIsBlockedByMe);
        byUsername.GetProperty("isBlockedByMe").GetBoolean().Should().Be(expectedIsBlockedByMe);
    }

    [Fact]
    public async Task Profile_OfAnUnblockedUser_ReportsNotBlocked()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");

        JsonElement body = await GetAsync($"/api/v1/users/{other.Id}");

        body.GetProperty("isBlockedByMe").GetBoolean().Should().BeFalse();
    }

    private async Task<(User Caller, User Blocked, User Visible)> ArrangeAsync(bool callerBlocked)
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User blocked = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User visible = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await (callerBlocked
            ? UserBlockTestData.AddBlockAsync(_factory, caller, blocked)
            : UserBlockTestData.AddBlockAsync(_factory, blocked, caller));

        return (caller, blocked, visible);
    }

    private async Task<JsonElement> GetAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<HttpStatusCode> StatusOfAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static JsonElement[] Items(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().ToArray();
    }
}
