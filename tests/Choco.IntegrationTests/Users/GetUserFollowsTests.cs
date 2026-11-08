using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserFollowsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownUserId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetUserFollowsTests(ChocoApiFactory factory)
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
    public async Task ListsTheRequestedDirectionAcrossPages_ExcludesDeletedUsers(string direction)
    {
        User target = await AddUserAsync("identity-id", "username");
        User first = await AddUserAsync("identity-id-2", "username-2");
        User second = await AddUserAsync("identity-id-3", "username-3");
        User deleted = await AddUserAsync("identity-id-4", "username-4");
        User opposite = await AddUserAsync("identity-id-5", "username-5");
        await _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(
                FollowInDirection(target, first, direction, now),
                FollowInDirection(target, second, direction, now),
                FollowInDirection(target, deleted, direction, now),
                FollowInDirection(opposite, target, direction, now));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await SoftDeleteAsync(deleted);
        User[] expected = new[] { first, second }.OrderBy(user => user.Id).ToArray();
        string path = $"/api/v1/users/{target.Id}/{direction}?limit=1";
        using HttpRequestMessage firstRequest = Request(path);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        JsonElement firstPage = await ReadPageAsync(firstResponse);
        string? cursor = firstPage.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrWhiteSpace();
        using HttpRequestMessage secondRequest = Request($"{path}&cursor={Uri.EscapeDataString(cursor!)}");
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        JsonElement secondPage = await ReadPageAsync(secondResponse);

        AssertSingleUser(firstPage, expected[0]);
        AssertSingleUser(secondPage, expected[1]);
        secondPage.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task ExistingUserWithoutFollows_ReturnsEmptyPage(string direction)
    {
        User target = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{direction}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement page = await ReadPageAsync(response);
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task UnknownUser_ReturnsNotFound(string direction)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request($"/api/v1/users/{UnknownUserId}/{direction}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertNotFoundAsync(response);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task DeletedUser_ReturnsNotFound(string direction)
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        await SoftDeleteAsync(target);
        using HttpRequestMessage request = Request($"/api/v1/users/{target.Id}/{direction}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertNotFoundAsync(response);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task InvalidCursor_ReturnsValidationProblem(string direction)
    {
        using HttpRequestMessage request = Request($"/api/v1/users/{UnknownUserId}/{direction}?cursor=invalid-cursor");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(400);
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("Cursor");
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task NoToken_ReturnsUnauthorized(string direction)
    {
        using HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/users/{UnknownUserId}/{direction}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, $"Display Name {username}", "https://example.com/avatar.png");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private Task<int> SoftDeleteAsync(User user)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private static UserFollow FollowInDirection(User target, User other, string direction, DateTimeOffset now)
    {
        return direction == "followers"
            ? UserFollow.Create(other.Id, target.Id, now).Value
            : UserFollow.Create(target.Id, other.Id, now).Value;
    }

    private static HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static async Task<JsonElement> ReadPageAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static void AssertSingleUser(JsonElement page, User expected)
    {
        JsonElement item = page.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "username", "displayName", "avatarUrl");
        item.GetProperty("id").GetGuid().Should().Be(expected.Id);
        item.GetProperty("username").GetString().Should().Be(expected.Username);
        item.GetProperty("displayName").GetString().Should().Be(expected.DisplayName);
        item.GetProperty("avatarUrl").GetString().Should().Be(expected.AvatarUrl);
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(404);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.NotFound");
    }
}
