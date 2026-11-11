using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserProfileTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetUserProfileTests(ChocoApiFactory factory)
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
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/users/00000000-0000-0000-0000-000000000001", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/avatar.png")]
    public async Task ExistingUserWithoutRelationships_ReturnsProfileWithZeroFollowStatus(string? avatarUrl)
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", avatarUrl, "Display Name 2");
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

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
        body.GetProperty("isBlockedByMe").GetBoolean().Should().BeFalse();
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
        await _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(
                UserFollow.Create(caller.Id, target.Id, now).Value,
                UserFollow.Create(other.Id, target.Id, now).Value,
                UserFollow.Create(target.Id, other.Id, now).Value,
                UserFollow.Create(other.Id, caller.Id, now).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
        body.GetProperty("followerCount").GetInt32().Should().Be(2);
        body.GetProperty("followingCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DeletedFollowers_AreNotCounted()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);
        User deleted = await AddUserAsync("identity-id-3", "username-3", null);
        await _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(
                UserFollow.Create(caller.Id, target.Id, now).Value,
                UserFollow.Create(deleted.Id, target.Id, now).Value,
                UserFollow.Create(target.Id, deleted.Id, now).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == deleted.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("followerCount").GetInt32().Should().Be(1);
        body.GetProperty("followingCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task FollowThenUnfollow_IsReflectedByTheNextProfileRequest()
    {
        await AddUserAsync("identity-id", "username", null);
        User target = await AddUserAsync("identity-id-2", "username-2", null);

        JsonElement afterFollow = await SendAndReadAsync(HttpMethod.Put, $"/api/v1/users/{target.Id}/follow", $"/api/v1/users/{target.Id}");
        JsonElement afterUnfollow = await SendAndReadAsync(HttpMethod.Delete, $"/api/v1/users/{target.Id}/follow", $"/api/v1/users/{target.Id}");

        afterFollow.GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
        afterFollow.GetProperty("followerCount").GetInt32().Should().Be(1);
        afterUnfollow.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        afterUnfollow.GetProperty("followerCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username", null);
        using HttpRequestMessage request = Request("/api/v1/users/00000000-0000-0000-0000-000000000001");

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
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task EmptyUserId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username", null);
        using HttpRequestMessage request = Request($"/api/v1/users/{Guid.Empty}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be("Id");
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
        using HttpRequestMessage request = Request("/api/v1/users/00000000-0000-0000-0000-000000000001");

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
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

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
        using HttpRequestMessage request = Request("/api/v1/users/00000000-0000-0000-0000-000000000001");

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
        using HttpRequestMessage request = Request("/api/v1/users/00000000-0000-0000-0000-000000000001");

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

    private async Task<JsonElement> SendAndReadAsync(HttpMethod method, string actionPath, string profilePath)
    {
        using HttpRequestMessage action = new(method, actionPath);
        action.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        using HttpResponseMessage actionResponse = await _client.SendAsync(action, TestContext.Current.CancellationToken);
        actionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpRequestMessage request = Request(profilePath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
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
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("postCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task PublishingThenDeletingAPost_IsReflectedByTheNextProfileRequest()
    {
        User target = await AddUserAsync("identity-id", "username", null);

        int before = await ReadPostCountAsync($"/api/v1/users/{target.Id}");
        using HttpRequestMessage create = Request("/api/v1/posts", HttpMethod.Post);
        create.Content = JsonContent.Create(new { content = "content" });
        using HttpResponseMessage created = await _client.SendAsync(create, TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid postId = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        int afterPublish = await ReadPostCountAsync($"/api/v1/users/{target.Id}");
        using HttpRequestMessage delete = Request($"/api/v1/posts/{postId}", HttpMethod.Delete);
        using HttpResponseMessage deleted = await _client.SendAsync(delete, TestContext.Current.CancellationToken);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        int afterDelete = await ReadPostCountAsync($"/api/v1/users/{target.Id}");

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
