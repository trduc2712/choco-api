using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Users;

public sealed class UserListFollowStatusTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset FirstFollowedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UserListFollowStatusTests(ChocoApiFactory factory)
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
    [InlineData("search")]
    [InlineData("suggestions")]
    public async Task EachUser_SaysWhetherTheCallerFollowsThem(string list)
    {
        User caller = await AddUserAsync("identity-id", "username-caller");
        User subject = await AddUserAsync("identity-id-2", "username-subject");
        User followedByCaller = await AddUserAsync("identity-id-3", "target-a");
        User notFollowed = await AddUserAsync("identity-id-4", "target-b");
        User alsoNotFollowed = await AddUserAsync("identity-id-5", "target-c");
        await FollowAsync(caller, followedByCaller);
        await AddListRelationsAsync(list, subject, followedByCaller, notFollowed, alsoNotFollowed);

        JsonElement[] items = await GetItemsAsync(list, subject);

        items.ToDictionary(item => item.GetProperty("username").GetString()!, item => item.GetProperty("isFollowedByMe").GetBoolean())
            .Should().BeEquivalentTo(new Dictionary<string, bool>
            {
                ["target-a"] = true,
                ["target-b"] = false,
                ["target-c"] = false,
            });
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task CallersOwnRow_IsNeverFollowedByMe(string list)
    {
        User caller = await AddUserAsync("identity-id", "username-caller");
        User subject = await AddUserAsync("identity-id-2", "username-subject");
        await AddListRelationsAsync(list, subject, caller);

        JsonElement[] items = await GetItemsAsync(list, subject);

        JsonElement item = items.Should().ContainSingle().Subject;
        item.GetProperty("username").GetString().Should().Be("username-caller");
        item.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task EachPage_ShowsItsOwnStatuses(string list)
    {
        User caller = await AddUserAsync("identity-id", "username-caller");
        User subject = await AddUserAsync("identity-id-2", "username-subject");
        User newest = await AddUserAsync("identity-id-3", "target-a");
        User middle = await AddUserAsync("identity-id-4", "target-b");
        User oldest = await AddUserAsync("identity-id-5", "target-c");
        await AddListRelationsAsync(list, subject, oldest, middle, newest);
        await FollowAsync(caller, oldest);
        await FollowAsync(caller, newest);
        using HttpRequestMessage firstRequest = Request(HttpMethod.Get, $"/api/v1/users/{subject.Id}/{list}?limit=2");
        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        JsonElement firstPage = await firstResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string cursor = firstPage.GetProperty("nextCursor").GetString()!;
        using HttpRequestMessage secondRequest = Request(HttpMethod.Get, $"/api/v1/users/{subject.Id}/{list}?limit=2&cursor={cursor}");

        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        JsonElement secondPage = await secondResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        firstPage.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("username").GetString(), item.GetProperty("isFollowedByMe").GetBoolean()))
            .Should().Equal(("target-a", true), ("target-b", false));
        secondPage.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("username").GetString(), item.GetProperty("isFollowedByMe").GetBoolean()))
            .Should().Equal(("target-c", true));
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    [InlineData("search")]
    [InlineData("suggestions")]
    public async Task FollowThenUnfollow_IsReflectedByTheNextRequest(string list)
    {
        await AddUserAsync("identity-id", "username-caller");
        User subject = await AddUserAsync("identity-id-2", "username-subject");
        User target = await AddUserAsync("identity-id-3", "target-a");
        await AddListRelationsAsync(list, subject, target);

        JsonElement before = (await GetItemsAsync(list, subject)).Should().ContainSingle().Subject;
        await SendFollowAsync(HttpMethod.Put, target);
        JsonElement afterFollow = (await GetItemsAsync(list, subject)).Should().ContainSingle().Subject;
        await SendFollowAsync(HttpMethod.Delete, target);
        JsonElement afterUnfollow = (await GetItemsAsync(list, subject)).Should().ContainSingle().Subject;

        before.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
        afterFollow.GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
        afterUnfollow.GetProperty("isFollowedByMe").GetBoolean().Should().BeFalse();
    }

    private async Task<JsonElement[]> GetItemsAsync(string list, User subject)
    {
        string path = list switch
        {
            "followers" => $"/api/v1/users/{subject.Id}/followers",
            "following" => $"/api/v1/users/{subject.Id}/following",
            "search" => "/api/v1/users/search?q=target",
            _ => "/api/v1/users/suggestions?q=target",
        };
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (list == "suggestions" ? body : body.GetProperty("items")).EnumerateArray().ToArray();
    }

    private async Task SendFollowAsync(HttpMethod method, User target)
    {
        using HttpRequestMessage request = Request(method, $"/api/v1/users/{target.Id}/follow");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private Task AddListRelationsAsync(string list, User subject, params User[] others)
    {
        return list switch
        {
            "followers" => FollowAllAsync(others.Select(other => (other, subject)).ToArray()),
            "following" => FollowAllAsync(others.Select(other => (subject, other)).ToArray()),
            _ => Task.CompletedTask,
        };
    }

    private Task FollowAsync(User follower, User followed)
    {
        return FollowAllAsync([(follower, followed)]);
    }

    private Task FollowAllAsync((User Follower, User Followed)[] pairs)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            int hours = 0;
            foreach ((User follower, User followed) in pairs)
            {
                db.Add(UserFollow.Create(follower.Id, followed.Id, FirstFollowedAt.AddHours(++hours)).Value);
            }

            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
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

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }
}
