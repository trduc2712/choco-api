using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserFollowListTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset FirstFollowedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UnknownUserId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetUserFollowListTests(ChocoApiFactory factory)
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
    [InlineData("followers")]
    [InlineData("following")]
    public async Task NoToken_ReturnsUnauthorized(string list)
    {
        using HttpResponseMessage response = await _client.GetAsync($"/api/v1/users/{UnknownUserId}/{list}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("followers", "username-5,username-4,username-3")]
    [InlineData("following", "username-3,username-4,username-5")]
    public async Task ExistingUser_ReturnsActiveUsersMostRecentFirst(string list, string expectedUsernames)
    {
        User target = await AddRelationshipsAsync();
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("items", "nextCursor");
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("username").GetString()).Should().Equal(expectedUsernames.Split(','));
        items[0].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("username", "displayName", "avatarUrl", "isFollowedByMe");
        items[0].GetProperty("displayName").GetString().Should().Be("Display Name");
        items[0].GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar.png");
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("followers", "username-5,username-4", "username-3")]
    [InlineData("following", "username-3,username-4", "username-5")]
    public async Task Pages_FollowNextCursorUntilTheLastPage(string list, string expectedFirstPage, string expectedLastPage)
    {
        User target = await AddRelationshipsAsync();
        using HttpRequestMessage firstRequest = Request($"/api/v1/users/{target.Id}/{list}?limit=2");

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("username").GetString())
            .Should().Equal(expectedFirstPage.Split(','));
        string cursor = first.GetProperty("nextCursor").GetString()!;
        using HttpRequestMessage lastRequest = Request($"/api/v1/users/{target.Id}/{list}?limit=2&cursor={cursor}");

        using HttpResponseMessage lastResponse = await _client.SendAsync(lastRequest, TestContext.Current.CancellationToken);

        lastResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement last = await lastResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        last.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("username").GetString())
            .Should().Equal(expectedLastPage.Split(','));
        last.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task ExistingUserWithoutRelationships_ReturnsEmptyPage(string list)
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task UnknownUser_ReturnsNotFound(string list)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/users/{UnknownUserId}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task DeletedUser_ReturnsNotFound(string list)
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        await DeleteUserAsync(target);
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserNotFoundProblemAsync(response);
    }

    [Theory]
    [InlineData("followers", "/00000000-0000-0000-0000-000000000000/", "", "UserId")]
    [InlineData("following", "/00000000-0000-0000-0000-000000000000/", "", "UserId")]
    [InlineData("followers", "/00000000-0000-0000-0000-000000000001/", "?limit=0", "Limit")]
    [InlineData("followers", "/00000000-0000-0000-0000-000000000001/", "?limit=51", "Limit")]
    [InlineData("following", "/00000000-0000-0000-0000-000000000001/", "?limit=0", "Limit")]
    [InlineData("following", "/00000000-0000-0000-0000-000000000001/", "?limit=51", "Limit")]
    [InlineData("followers", "/00000000-0000-0000-0000-000000000001/", "?cursor=invalid-cursor", "Cursor")]
    [InlineData("following", "/00000000-0000-0000-0000-000000000001/", "?cursor=invalid-cursor", "Cursor")]
    public async Task InvalidInput_ReturnsValidationProblem(string list, string userPath, string query, string invalidProperty)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/users{userPath}{list}{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be(invalidProperty);
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task WithoutLimit_ReturnsTwentyItemsAndNextCursor(string list)
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        List<User> others = [];
        for (int number = 3; number <= 23; number++)
        {
            others.Add(User.Create($"identity-id-{number}", $"username-{number}", "Display Name", null));
        }

        await _factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(others);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(others.Select((other, index) => list == "followers"
                ? Follow(other, target, index)
                : Follow(target, other, index)));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("items").GetArrayLength().Should().Be(20);
        body.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task DeletedCaller_ReturnsUnauthorized(string list)
    {
        User caller = await AddUserAsync("identity-id", "username");
        await DeleteUserAsync(caller);
        using HttpRequestMessage request = Request($"/api/v1/users/{UnknownUserId}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("code").GetString().Should().Be("User.Deleted");
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task NewCaller_CreatesLocalAccountAndReturnsList(string list)
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
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
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{list}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        User caller = await _factory.ExecuteDbContextAsync(db => db.Set<User>().AsNoTracking()
            .SingleAsync(user => user.IdentityId == "identity-id", TestContext.Current.CancellationToken));
        caller.Username.Should().Be("username");
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    private async Task<User> AddRelationshipsAsync()
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        User first = await AddUserAsync("identity-id-3", "username-3");
        User second = await AddUserAsync("identity-id-4", "username-4");
        User third = await AddUserAsync("identity-id-5", "username-5");
        User deleted = await AddUserAsync("identity-id-6", "username-6");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(
                Follow(first, target, 1),
                Follow(second, target, 2),
                Follow(third, target, 3),
                Follow(deleted, target, 4),
                Follow(target, third, 1),
                Follow(target, second, 2),
                Follow(target, first, 3),
                Follow(target, deleted, 4));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await DeleteUserAsync(deleted);
        return target;
    }

    private static UserFollow Follow(User follower, User followed, int hours)
    {
        return UserFollow.Create(follower.Id, followed.Id, FirstFollowedAt.AddHours(hours)).Value;
    }

    private Task<int> DeleteUserAsync(User user)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(candidate => candidate.Id == user.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private static async Task ShouldBeUserNotFoundProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails body = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(404);
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.NotFound");
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", "https://example.com/avatar.png");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private static HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }
}
