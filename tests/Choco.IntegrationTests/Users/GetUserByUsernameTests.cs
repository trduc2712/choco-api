using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserByUsernameTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetUserByUsernameTests(ChocoApiFactory factory)
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
    public async Task BrowserPreflight_AllowsConfiguredOriginAndAuthorizationHeader()
    {
        const string origin = "https://choco-web.example.test";
        await using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = origin })));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new("https://localhost"),
            AllowAutoRedirect = false,
        });
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/users/by-username/unknown-username");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(origin);
        response.Headers.GetValues("Access-Control-Allow-Headers").Should().Contain("authorization");
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/users/by-username/unknown-username", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/avatar.png")]
    public async Task ExistingUserWithoutRelationships_ReturnsProfileWithZeroFollowStatus(string? avatarUrl)
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", avatarUrl, "Display Name 2");
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "username", "displayName", "avatarUrl", "bio", "isFollowedByMe", "isBlockedByMe", "followerCount", "followingCount", "postCount");
        body.GetProperty("bio").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("id").GetGuid().Should().Be(target.Id);
        body.GetProperty("username").GetString().Should().Be(target.Username);
        body.GetProperty("displayName").GetString().Should().Be("Display Name 2");
        body.GetProperty("avatarUrl").GetString().Should().Be(avatarUrl);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        body.GetProperty("followerCount").GetInt32().Should().Be(0);
        body.GetProperty("followingCount").GetInt32().Should().Be(0);
        body.GetProperty("postCount").GetInt32().Should().Be(0);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FollowRelationships_AreReflectedInFollowStatusAndCounts()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        User other = await AddUserAsync("identity-id-3", "username-3", null);
        User deleted = await AddUserAsync("identity-id-4", "username-4", null);
        await AddFollowsAsync(
            (caller, target), (other, target), (deleted, target), (target, other), (target, deleted), (other, caller));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == deleted.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
        body.GetProperty("followerCount").GetInt32().Should().Be(2);
        body.GetProperty("followingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TargetFollowingOnlyTheCaller_ReturnsNotFollowedByMe()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        await AddFollowsAsync((target, caller));
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        body.GetProperty("followerCount").GetInt32().Should().Be(0);
        body.GetProperty("followingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CallersOwnProfile_ReturnsCountsAndNotFollowedByMe()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        User other = await AddUserAsync("identity-id-2", "username-2", null);
        await AddFollowsAsync((other, caller), (caller, other));
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{caller.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        body.GetProperty("followerCount").GetInt32().Should().Be(1);
        body.GetProperty("followingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FollowedTarget_ReturnsFollowStatusTrueAndUpdatedCounts()
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        using HttpRequestMessage follow = new(HttpMethod.Put, $"/api/v1/users/{target.Id}/follow");
        follow.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        using HttpResponseMessage followResponse = await _client.SendAsync(follow, TestContext.Current.CancellationToken);
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        followResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
        body.GetProperty("followerCount").GetInt32().Should().Be(1);
        body.GetProperty("followingCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UnfollowedTarget_ReturnsFollowStatusFalseAndUpdatedCounts()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        await AddFollowsAsync((caller, target));
        using HttpRequestMessage unfollow = new(HttpMethod.Delete, $"/api/v1/users/{target.Id}/follow");
        unfollow.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        using HttpResponseMessage unfollowResponse = await _client.SendAsync(unfollow, TestContext.Current.CancellationToken);
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        unfollowResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        body.GetProperty("followerCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username", null);
        using HttpRequestMessage request = Request("/api/v1/users/by-username/unknown-username");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == target.Id));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task DifferentUsernameCase_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username", null);
        await AddUserAsync("identity-id-2", "username-2", null);
        using HttpRequestMessage request = Request("/api/v1/users/by-username/USERNAME-2");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task ReusedDeletedUsername_ReturnsActiveProfile()
    {
        await AddUserAsync("identity-id", "username", null);
        User deleted = await AddUserAsync("identity-id-2", "username-2", null);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == deleted.Id));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        User active = await AddUserAsync("identity-id-3", "username-2", null, "Display Name 3");
        using HttpRequestMessage request = Request("/api/v1/users/by-username/username-2");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(active.Id);
        body.GetProperty("displayName").GetString().Should().Be("Display Name 3");
    }

    [Fact]
    public async Task TooLongUsername_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username", null);
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{new string('u', User.UsernameMaxLength + 1)}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be("Username");
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request("/api/v1/users/by-username/unknown-username");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("code").GetString().Should().Be("User.Deleted");
    }

    [Fact]
    public async Task NewCaller_CreatesLocalAccountAndReturnsRequestedProfile()
    {
        User target = await AddUserAsync("identity-id-2", "username-2", null, "Display Name 2");
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
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("username").GetString().Should().Be("username-2");
        body.GetProperty("displayName").GetString().Should().Be("Display Name 2");
        body.GetProperty("avatarUrl").GetString().Should().BeNull();
        User caller = await _factory.ExecuteDbContextAsync(db => db.Set<User>().AsNoTracking()
            .SingleAsync(user => user.IdentityId == "identity-id", TestContext.Current.CancellationToken));
        body.GetProperty("id").GetGuid().Should().Be(target.Id);
        caller.Username.Should().Be("username");
        caller.DisplayName.Should().Be("first-name last-name");
        caller.AvatarUrl.Should().Be("https://example.com/avatar.png");
        HttpRequestMessage outgoing = _factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        outgoing.Method.Should().Be(HttpMethod.Get);
        outgoing.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id"));
        outgoing.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", ChocoApiFactory.IdentityProviderSecretKey));
    }

    [Fact]
    public async Task CallerMissingFromIdentityProvider_ReturnsUnauthorizedAndCreatesNoUser()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpRequestMessage request = Request("/api/v1/users/by-username/unknown-username");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("code").GetString().Should().Be("User.Deleted");
        (await _factory.ExecuteDbContextAsync(db => db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task NewCallerWithoutUsername_ReturnsValidationProblemAndCreatesNoUser()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = (string?)null,
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request("/api/v1/users/by-username/unknown-username");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be("Username");
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
        (await _factory.ExecuteDbContextAsync(db => db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    private static async Task ShouldBeUserNotFoundProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails body = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(404);
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.NotFound");
    }

    private async Task<User> AddUserAsync(string identityId, string username, string? avatarUrl, string displayName = "Display Name")
    {
        User user = User.Create(identityId, username, displayName, avatarUrl);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private Task<int> AddFollowsAsync(params (User Follower, User Followed)[] pairs)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(pairs.Select(pair => UserFollow.Create(pair.Follower.Id, pair.Followed.Id, now).Value));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task PostCount_CountsTheUsersPostsAndSkipsDeletedAndOthersPosts()
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        User other = await AddUserAsync("identity-id-3", "username-3", null);
        Post deleted = Post.Create("content-3", target.Id);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(
                Post.Create("content-1", target.Id),
                Post.Create("content-2", target.Id),
                deleted,
                Post.Create("content-4", other.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(post => post.Id == deleted.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/by-username/{target.Username}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("postCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task PublishingThenDeletingAPost_IsReflectedByTheNextProfileRequest()
    {
        User target = await AddUserAsync("identity-id", "username", null);

        int before = await ReadPostCountAsync($"/api/v1/users/by-username/{target.Username}");
        using HttpRequestMessage create = Request("/api/v1/posts", HttpMethod.Post);
        create.Content = JsonContent.Create(new { content = "content" });
        using HttpResponseMessage created = await _client.SendAsync(create, TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid postId = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        int afterPublish = await ReadPostCountAsync($"/api/v1/users/by-username/{target.Username}");
        using HttpRequestMessage delete = Request($"/api/v1/posts/{postId}", HttpMethod.Delete);
        using HttpResponseMessage deleted = await _client.SendAsync(delete, TestContext.Current.CancellationToken);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        int afterDelete = await ReadPostCountAsync($"/api/v1/users/by-username/{target.Username}");

        before.Should().Be(0);
        afterPublish.Should().Be(1);
        afterDelete.Should().Be(0);
    }

    private async Task<int> ReadPostCountAsync(string path)
    {
        using HttpRequestMessage request = Request(path);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("postCount").GetInt32();
    }

    private static HttpRequestMessage Request(string path, HttpMethod? method = null)
    {
        HttpRequestMessage request = new(method ?? HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }
}
